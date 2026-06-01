using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Neo4j.Driver;

namespace AIRecruitment.Api.Services;

/// <summary>
/// 图谱自演化引擎 — 基于多源采集数据的真实技能漂移检测与图谱更新。
/// 
/// 核心流程：
///   1. 从数据库采集的岗位数据中提取技能频率
///   2. 与上一周期快照对比，检测新兴/衰退/稳定技能
///   3. 将真实数据写入 Neo4j 图谱（替代原来的随机数假数据）
///   4. 生成演化快照，供前端时间轴展示
/// 
/// 检测算法：
///   - 新兴技能：当前周期频率 > 上周期频率 × 1.5 且上周期频率 < 阈值
///   - 衰退技能：当前周期频率 < 上周期频率 × 0.6 且当前频率 < 阈值
///   - 强势技能：连续两个周期频率 > 高阈值
///   - 稳定技能：变化幅度 < 20%
/// </summary>
public class GraphEvolutionService
{
    private readonly KnowledgeGraphService _graph;
    private readonly AppDbContext _db;
    private readonly IAIService _ai;
    private readonly ILogger<GraphEvolutionService> _logger;

    // 漂移检测阈值
    private const double EmergingGrowthThreshold = 1.5;   // 频率增长 > 50% 视为新兴
    private const double DecliningDropThreshold = 0.6;     // 频率下降 > 40% 视为衰退
    private const int EmergingMinCurrent = 3;               // 新兴技能当前至少出现 3 次
    private const int DecliningMaxCurrent = 5;              // 衰退技能当前不超过 5 次
    private const int StrongSkillMinFrequency = 10;         // 强势技能最低频率
    private const double StableChangeThreshold = 0.2;       // 变化 < 20% 视为稳定

    public GraphEvolutionService(
        KnowledgeGraphService graph,
        AppDbContext db,
        IAIService ai,
        ILogger<GraphEvolutionService> logger)
    {
        _graph = graph;
        _db = db;
        _ai = ai;
        _logger = logger;
    }

    // ═══════════════════════════════════════════
    // 主入口：执行完整演化周期
    // ═══════════════════════════════════════════

    /// <summary>
    /// 执行完整演化周期：
    /// 采集数据 → 检测漂移 → 更新图谱 → 生成快照 → 返回报告
    /// </summary>
    public async Task<EvolutionCycleReport> RunEvolutionCycleAsync()
    {
        var report = new EvolutionCycleReport { StartedAt = DateTime.UtcNow };
        var now = DateTime.UtcNow;
        var period = $"{now.Year}-Q{(now.Month - 1) / 3 + 1}";

        try
        {
            _logger.LogInformation("[Evolution] 周期开始: {period}", period);

            // Step 1: 从数据库提取所有岗位的技能频率
            var currentSkills = await ExtractSkillFrequenciesFromDBAsync();
            report.TotalSkillsExtracted = currentSkills.Count;
            report.TotalJobsAnalyzed = await _db.Jobs.CountAsync(j => j.Status == 1);
            _logger.LogInformation("[Evolution] 提取 {n} 个技能，来自 {m} 个岗位",
                currentSkills.Count, report.TotalJobsAnalyzed);

            // Step 2: 获取上一周期快照
            var prevPeriod = GetPreviousPeriod(period);
            var prevSnapshot = await GetSnapshotSkillMapAsync(prevPeriod);
            _logger.LogInformation("[Evolution] 上一周期 {period}: {n} 个技能",
                prevPeriod, prevSnapshot.Count);

            // Step 3: 技能漂移检测
            var drift = DetectSkillDrift(currentSkills, prevSnapshot, prevPeriod);
            report.Drift = drift;
            _logger.LogInformation("[Evolution] 漂移检测: ▲{emerging}新兴 ▼{declining}衰退 →{stable}稳定",
                drift.Emerging.Count, drift.Declining.Count, drift.Stable.Count);

            // Step 4: 更新 Neo4j 图谱（用真实数据）
            await UpdateGraphWithRealDataAsync(currentSkills);
            report.GraphUpdated = true;

            // Step 5: 生成新快照
            var snapshots = await _graph.TakeSnapshotAsync(_db, period);
            report.SnapshotsCreated = snapshots.Count;

            // Step 6: 调用 AI 生成演化摘要（可选）
            try
            {
                var summaryPrompt = BuildEvolutionSummaryPrompt(drift, period);
                var aiSummaryTask = _ai.ChatAsync(summaryPrompt);
                report.AISummary = await Task.WhenAny(aiSummaryTask, Task.Delay(8000)) == aiSummaryTask
                    ? await aiSummaryTask : null;
            }
            catch { report.AISummary = null; }

            report.CompletedAt = DateTime.UtcNow;
            report.Success = true;
            _logger.LogInformation("[Evolution] 周期完成: {period}, 耗时 {elapsed:F1}s",
                period, (report.CompletedAt - report.StartedAt).TotalSeconds);
        }
        catch (Exception ex)
        {
            report.Success = false;
            report.ErrorMessage = ex.Message;
            _logger.LogError(ex, "[Evolution] 周期失败");
        }

        return report;
    }

    // ═══════════════════════════════════════════
    // 数据提取：从数据库岗位中提取真实技能频率
    // ═══════════════════════════════════════════

    /// <summary>
    /// 从数据库中所有活跃岗位提取技能 → 频率映射。
    /// 使用 KnownSkills 词表做精确匹配。
    /// </summary>
    public async Task<Dictionary<string, int>> ExtractSkillFrequenciesFromDBAsync()
    {
        var frequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var allJobs = await _db.Jobs
            .Where(j => j.Status == 1)
            .Select(j => new { j.Title, j.Requirements, j.JD })
            .ToListAsync();

        foreach (var job in allJobs)
        {
            var text = $"{job.Title} {job.Requirements} {job.JD}";
            foreach (var skill in AccuracyTestData.KnownSkills)
            {
                if (text.Contains(skill, StringComparison.OrdinalIgnoreCase))
                {
                    frequencies[skill] = frequencies.GetValueOrDefault(skill) + 1;
                }
            }
        }

        return frequencies;
    }

    // ═══════════════════════════════════════════
    // 核心算法：技能漂移检测
    // ═══════════════════════════════════════════

    /// <summary>
    /// 对比当前技能频率与上周期快照，分类每个技能的演化状态。
    /// </summary>
    public SkillDriftReport DetectSkillDrift(
        Dictionary<string, int> currentFreq,
        Dictionary<string, int> previousFreq,
        string previousPeriod)
    {
        var report = new SkillDriftReport { PreviousPeriod = previousPeriod };

        var allSkills = new HashSet<string>(
            currentFreq.Keys.Union(previousFreq.Keys), StringComparer.OrdinalIgnoreCase);

        foreach (var skill in allSkills)
        {
            var cur = currentFreq.GetValueOrDefault(skill);
            var prev = previousFreq.GetValueOrDefault(skill);
            var change = prev > 0 ? (double)(cur - prev) / prev : (cur > 0 ? 1.0 : 0);
            var ratio = prev > 0 ? (double)cur / prev : (cur > 0 ? double.PositiveInfinity : 1.0);

            var entry = new SkillDriftEntry
            {
                Skill = skill,
                CurrentFrequency = cur,
                PreviousFrequency = prev,
                ChangePercent = Math.Round(change * 100, 1),
                PreviousPeriod = previousPeriod
            };

            if (cur == 0 && prev > 0)
            {
                entry.Status = "已消失";
                report.Disappeared.Add(entry);
            }
            else if (prev == 0 && cur > 0)
            {
                entry.Status = "新增";
                report.NewSkills.Add(entry);
            }
            else if (ratio >= EmergingGrowthThreshold && cur >= EmergingMinCurrent)
            {
                entry.Status = "新兴";
                report.Emerging.Add(entry);
            }
            else if (ratio <= DecliningDropThreshold && cur <= DecliningMaxCurrent)
            {
                entry.Status = "衰退";
                report.Declining.Add(entry);
            }
            else if (cur >= StrongSkillMinFrequency && prev >= StrongSkillMinFrequency)
            {
                entry.Status = "强势";
                report.Strong.Add(entry);
            }
            else if (Math.Abs(change) <= StableChangeThreshold)
            {
                entry.Status = "稳定";
                report.Stable.Add(entry);
            }
            else
            {
                entry.Status = change > 0 ? "增长" : "减弱";
                report.Changing.Add(entry);
            }
        }

        // 排序
        report.Emerging = report.Emerging.OrderByDescending(e => e.ChangePercent).ToList();
        report.Declining = report.Declining.OrderBy(e => e.ChangePercent).ToList();
        report.Strong = report.Strong.OrderByDescending(e => e.CurrentFrequency).ToList();
        report.NewSkills = report.NewSkills.OrderByDescending(e => e.CurrentFrequency).ToList();

        return report;
    }

    // ═══════════════════════════════════════════
    // 图谱更新：将真实数据写入 Neo4j
    // ═══════════════════════════════════════════

    /// <summary>
    /// 用真实数据更新 Neo4j 图谱中的技能权重。
    /// 按岗位分组，计算每岗位各技能的出现次数作为权重。
    /// </summary>
    public async Task UpdateGraphWithRealDataAsync(Dictionary<string, int> globalFrequencies)
    {
        var allJobs = await _db.Jobs
            .Where(j => j.Status == 1)
            .Select(j => new { j.JobId, j.Title, j.Requirements, j.JD })
            .ToListAsync();

        // 按岗位标题分组
        var jobGroups = allJobs.GroupBy(j => NormalizeJobTitle(j.Title));

        foreach (var group in jobGroups)
        {
            var jobTitle = group.Key;
            try
            {
                // 合并该岗位组的所有文本
                var combinedText = string.Join(" ", group.SelectMany(j =>
                    new[] { j.Requirements, j.JD }));

                // 提取技能 + 频率作为权重
                var skillWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var skill in AccuracyTestData.KnownSkills)
                {
                    var count = CountOccurrences(combinedText, skill);
                    if (count > 0) skillWeights[skill] = count;
                }

                // 写入 Neo4j
                await _graph.UpsertJobSkillsWithWeightsAsync(jobTitle, skillWeights);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[Evolution] 更新图谱失败 {job}: {msg}", jobTitle, ex.Message);
            }
        }

        _logger.LogInformation("[Evolution] 已更新 {n} 个岗位的图谱数据", jobGroups.Count());
    }

    // ═══════════════════════════════════════════
    // 演化时间轴查询
    // ═══════════════════════════════════════════

    /// <summary>获取所有历史快照的演化时间轴</summary>
    public async Task<EvolutionTimeline> GetEvolutionTimelineAsync()
    {
        var snapshots = await _db.GraphSnapshots
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var periods = snapshots.Select(s => s.Period).Distinct().OrderBy(p => p).ToList();
        var timeline = new EvolutionTimeline { Periods = periods, Snapshots = new() };

        foreach (var period in periods)
        {
            var periodSnapshots = snapshots
                .Where(s => s.Period == period)
                .ToList();

            var jobSkills = new Dictionary<string, List<SkillWeightItem>>();
            foreach (var snap in periodSnapshots)
            {
                try
                {
                    var skills = JsonConvert.DeserializeObject<List<SkillWeightItem>>(snap.SkillsJson) ?? new();
                    jobSkills[snap.JobName] = skills;
                }
                catch { }
            }

            timeline.Snapshots[period] = new PeriodSnapshot
            {
                Period = period,
                TakenAt = periodSnapshots.FirstOrDefault()?.CreatedAt ?? DateTime.MinValue,
                JobCount = periodSnapshots.Count,
                Jobs = jobSkills
            };
        }

        // 计算相邻周期之间的差异
        for (int i = 1; i < periods.Count; i++)
        {
            var prevPeriod = periods[i - 1];
            var currPeriod = periods[i];

            if (timeline.Snapshots.TryGetValue(prevPeriod, out var prev) &&
                timeline.Snapshots.TryGetValue(currPeriod, out var curr))
            {
                var changes = ComputePeriodDiff(prev.Jobs, curr.Jobs);
                timeline.Snapshots[currPeriod].ChangesFromPrevious = changes;
            }
        }

        return timeline;
    }

    /// <summary>追踪单个技能在多个周期中的生命周期</summary>
    public async Task<SkillLifecycle> GetSkillLifecycleAsync(string skillName)
    {
        var snapshots = await _db.GraphSnapshots
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        var periods = snapshots.Select(s => s.Period).Distinct().OrderBy(p => p).ToList();
        var lifecycle = new SkillLifecycle
        {
            Skill = skillName,
            DataPoints = new()
        };

        foreach (var period in periods)
        {
            var periodSnaps = snapshots.Where(s => s.Period == period);
            var totalWeight = 0.0;
            var jobCount = 0;

            foreach (var snap in periodSnaps)
            {
                try
                {
                    var skills = JsonConvert.DeserializeObject<List<SkillWeightItem>>(snap.SkillsJson) ?? new();
                    var match = skills.FirstOrDefault(s =>
                        s.Skill.Equals(skillName, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        totalWeight += match.Weight;
                        jobCount++;
                    }
                }
                catch { }
            }

            lifecycle.DataPoints.Add(new SkillLifecyclePoint
            {
                Period = period,
                AverageWeight = jobCount > 0 ? Math.Round(totalWeight / jobCount, 1) : 0,
                JobsWithSkill = jobCount
            });
        }

        return lifecycle;
    }

    // ═══════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════

    private async Task<Dictionary<string, int>> GetSnapshotSkillMapAsync(string period)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var snapshots = await _db.GraphSnapshots
            .Where(s => s.Period == period)
            .ToListAsync();

        foreach (var snap in snapshots)
        {
            try
            {
                var skills = JsonConvert.DeserializeObject<List<SkillWeightItem>>(snap.SkillsJson) ?? new();
                foreach (var sk in skills)
                {
                    map[sk.Skill] = map.GetValueOrDefault(sk.Skill) + (int)sk.Weight;
                }
            }
            catch { }
        }

        return map;
    }

    private static string GetPreviousPeriod(string currentPeriod)
    {
        // Format: "2026-Q2"
        var parts = currentPeriod.Split('-');
        if (parts.Length != 2) return currentPeriod;
        if (!int.TryParse(parts[0], out var year) || !int.TryParse(parts[1][1..], out var quarter))
            return currentPeriod;

        quarter--;
        if (quarter < 1) { quarter = 4; year--; }
        return $"{year}-Q{quarter}";
    }

    private static string NormalizeJobTitle(string title)
    {
        // 移除采集时间戳后缀
        var idx = title.IndexOf("（");
        if (idx > 0) title = title[..idx];

        // 移除数据源标注
        if (title.StartsWith("[数据源:"))
        {
            var end = title.IndexOf(']');
            if (end > 0) title = title[(end + 1)..].Trim();
        }

        // 移除前缀标签
        foreach (var prefix in new[] { "初级", "中级", "高级", "资深", "首席" })
            if (title.StartsWith(prefix)) title = title[prefix.Length..];

        return title.Trim();
    }

    private static int CountOccurrences(string text, string word) =>
        text.Split(new[] { ' ', ',', '，', '、', '\n', '\r', ';', '/' },
            StringSplitOptions.RemoveEmptyEntries)
            .Count(w => w.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static List<SkillChangeItem> ComputePeriodDiff(
        Dictionary<string, List<SkillWeightItem>> prevJobs,
        Dictionary<string, List<SkillWeightItem>> currJobs)
    {
        var changes = new List<SkillChangeItem>();
        var allJobs = prevJobs.Keys.Union(currJobs.Keys);

        foreach (var job in allJobs)
        {
            var prevSkills = prevJobs.GetValueOrDefault(job) ?? new();
            var currSkills = currJobs.GetValueOrDefault(job) ?? new();

            var prevMap = prevSkills.ToDictionary(s => s.Skill, s => s.Weight);
            var currMap = currSkills.ToDictionary(s => s.Skill, s => s.Weight);

            var allSkills = prevMap.Keys.Union(currMap.Keys);
            foreach (var skill in allSkills)
            {
                var pw = prevMap.GetValueOrDefault(skill);
                var cw = currMap.GetValueOrDefault(skill);

                string changeType;
                if (pw == 0 && cw > 0) changeType = "新增";
                else if (pw > 0 && cw == 0) changeType = "移除";
                else if (cw > pw * 1.3) changeType = "增强";
                else if (cw < pw * 0.7) changeType = "减弱";
                else continue; // 无明显变化则跳过

                changes.Add(new SkillChangeItem
                {
                    Job = job,
                    Skill = skill,
                    ChangeType = changeType,
                    OldWeight = pw,
                    NewWeight = cw
                });
            }
        }

        return changes;
    }

    private static string BuildEvolutionSummaryPrompt(SkillDriftReport drift, string period)
    {
        var emerging = drift.Emerging.Take(5).Select(e => $"{e.Skill}(+{e.ChangePercent}%)");
        var declining = drift.Declining.Take(3).Select(d => $"{d.Skill}({d.ChangePercent}%)");
        var strong = drift.Strong.Take(5).Select(s => s.Skill);

        return $@"你是招聘市场分析师。基于以下技能演化数据，写一段150字的{period}招聘市场技能趋势摘要：

🔥 新兴技能：{string.Join("、", emerging)}
📉 衰退技能：{string.Join("、", declining)}
💪 持续强势：{string.Join("、", strong)}
📊 分析岗位数：{drift.TotalSkills}

要求：简洁有力，突出关键趋势，说明对招聘策略的启示。";
    }
}

// ═══════════════════════════════════════════
// 数据模型
// ═══════════════════════════════════════════

public class EvolutionCycleReport
{
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalJobsAnalyzed { get; set; }
    public int TotalSkillsExtracted { get; set; }
    public bool GraphUpdated { get; set; }
    public int SnapshotsCreated { get; set; }
    public SkillDriftReport? Drift { get; set; }
    public string? AISummary { get; set; }
}

public class SkillDriftReport
{
    public string PreviousPeriod { get; set; } = "";
    public List<SkillDriftEntry> Emerging { get; set; } = new();
    public List<SkillDriftEntry> Declining { get; set; } = new();
    public List<SkillDriftEntry> Strong { get; set; } = new();
    public List<SkillDriftEntry> Stable { get; set; } = new();
    public List<SkillDriftEntry> NewSkills { get; set; } = new();
    public List<SkillDriftEntry> Disappeared { get; set; } = new();
    public List<SkillDriftEntry> Changing { get; set; } = new();
    public int TotalSkills => Emerging.Count + Declining.Count + Strong.Count + Stable.Count + NewSkills.Count + Disappeared.Count + Changing.Count;
}

public class SkillDriftEntry
{
    public string Skill { get; set; } = "";
    public string Status { get; set; } = "";
    public int CurrentFrequency { get; set; }
    public int PreviousFrequency { get; set; }
    public double ChangePercent { get; set; }
    public string PreviousPeriod { get; set; } = "";
}

public class EvolutionTimeline
{
    public List<string> Periods { get; set; } = new();
    public Dictionary<string, PeriodSnapshot> Snapshots { get; set; } = new();
}

public class PeriodSnapshot
{
    public string Period { get; set; } = "";
    public DateTime TakenAt { get; set; }
    public int JobCount { get; set; }
    public Dictionary<string, List<SkillWeightItem>> Jobs { get; set; } = new();
    public List<SkillChangeItem>? ChangesFromPrevious { get; set; }
}

public class SkillChangeItem
{
    public string Job { get; set; } = "";
    public string Skill { get; set; } = "";
    public string ChangeType { get; set; } = "";
    public double OldWeight { get; set; }
    public double NewWeight { get; set; }
}

public class SkillLifecycle
{
    public string Skill { get; set; } = "";
    public List<SkillLifecyclePoint> DataPoints { get; set; } = new();
}

public class SkillLifecyclePoint
{
    public string Period { get; set; } = "";
    public double AverageWeight { get; set; }
    public int JobsWithSkill { get; set; }
}

public class SkillWeightItem
{
    public string Skill { get; set; } = "";
    public double Weight { get; set; }
}
