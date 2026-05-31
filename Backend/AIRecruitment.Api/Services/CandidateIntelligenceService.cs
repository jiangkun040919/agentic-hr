using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;

namespace AIRecruitment.Api.Services;

/// <summary>
/// 候选人端智能服务 — 让求职者不再是"投了等通知"，而是看到自己的竞争力和成长路径。
/// 
/// 三大能力：
///   1. 个性化成长路径 — 计算到目标岗位的最短学习路径
///   2. 简历竞争力分析 — 投递后显示排名和匹配明细
///   3. 岗位匹配透明度 — 浏览岗位时即可看到即时匹配度
/// </summary>
public class CandidateIntelligenceService
{
    private readonly KnowledgeGraphService _graph;
    private readonly AppDbContext _db;
    private readonly IAIService _ai;
    private readonly ILogger<CandidateIntelligenceService> _logger;

    public CandidateIntelligenceService(KnowledgeGraphService graph, AppDbContext db,
        IAIService ai, ILogger<CandidateIntelligenceService> logger)
    {
        _graph = graph; _db = db; _ai = ai; _logger = logger;
    }

    // ═══════════════════════════════════════════
    // 1. 个性化成长路径
    // ═══════════════════════════════════════════

    /// <summary>
    /// 为候选人计算到达目标岗位的最短学习路径。
    /// 算法：通过 Neo4j 图谱遍历，找到从当前技能到目标技能的最短跳数。
    /// </summary>
    public async Task<CareerPathResult> GetCareerPathAsync(int candidateId, int targetJobId)
    {
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.CandidateId == candidateId);
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == targetJobId);
        if (candidate == null || job == null) throw new Exception("候选人或岗位不存在");

        var skillsText = await GetCandidateSkillsTextAsync(candidateId);
        var cSkills = ExtractSkills(skillsText);
        var jSkills = ExtractSkills($"{job.Requirements} {job.JD}");

        var result = new CareerPathResult
        {
            CandidateName = candidate.RealName,
            TargetJob = job.Title,
            CurrentSkills = cSkills,
            TargetSkills = jSkills,
            MatchedSkills = cSkills.Intersect(jSkills, StringComparer.OrdinalIgnoreCase).ToList(),
            MissingSkills = jSkills.Except(cSkills, StringComparer.OrdinalIgnoreCase).ToList(),
            Steps = new()
        };

        // 对每个缺失技能，通过 Neo4j 尝试找到学习路径
        foreach (var ms in result.MissingSkills.Take(5))
        {
            try
            {
                var graphPath = await _graph.GetLearningPathAsync(
                    string.Join(",", cSkills), job.Title);

                var step = graphPath.Steps.FirstOrDefault(s =>
                    s.Skill.Contains(ms, StringComparison.OrdinalIgnoreCase));

                result.Steps.Add(new CareerPathStep
                {
                    Skill = ms,
                    Priority = jSkills.Take(5).Contains(ms) ? "高" : "中",
                    EstimatedWeeks = EstimateWeeks(ms),
                    LearningSuggestion = step?.Suggestion
                        ?? $"系统学习 {ms}，推荐结合项目实践（{EstimateWeeks(ms)}周）",
                    Prerequisites = GetPrerequisites(ms)
                });
            }
            catch
            {
                result.Steps.Add(new CareerPathStep
                {
                    Skill = ms, Priority = "中", EstimatedWeeks = EstimateWeeks(ms),
                    LearningSuggestion = $"自学 {ms}，约{EstimateWeeks(ms)}周"
                });
            }
        }

        // 排序：优先 → 高优先，时间短 → 长
        result.Steps = result.Steps
            .OrderBy(s => s.Priority == "高" ? 0 : 1)
            .ThenBy(s => s.EstimatedWeeks)
            .ToList();

        result.CurrentMatchRate = jSkills.Count > 0
            ? Math.Round((double)result.MatchedSkills.Count / jSkills.Count * 100, 1) : 0;
        result.LearningWeeksTotal = result.Steps.Sum(s => s.EstimatedWeeks);
        result.ProjectedMatchRate = jSkills.Count > 0
            ? Math.Round((double)(result.MatchedSkills.Count + result.MissingSkills.Count) / jSkills.Count * 100, 1) : 100;

        // AI 学习建议
        try
        {
            var prompt = $"候选人技能：{string.Join("、",cSkills)}。目标岗位{job.Title}要求：{string.Join("、",jSkills)}。差距：{string.Join("、",result.MissingSkills.Take(3))}。给100字学习路径建议，聚焦最先学什么。";
            result.AIAdvice = await SafeCallAI(prompt);
        }
        catch { }

        return result;
    }

    /// <summary>推荐适合候选人的最优发展岗位（5个）</summary>
    public async Task<CareerRecommendations> RecommendCareerPathsAsync(int candidateId)
    {
        // 兼容前端传 CandidateId 或 UserId
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.CandidateId == candidateId)
                     ?? await _db.Candidates.FirstOrDefaultAsync(c => c.UserId == candidateId);
        if (candidate == null) throw new Exception("候选人不存在，请先完善个人资料");

        var actualId = candidate.CandidateId;
        var skillsText = await GetCandidateSkillsTextAsync(actualId);
        var cSkills = ExtractSkills(skillsText);

        var allJobs = await _db.Jobs.Where(j => j.Status == 1).ToListAsync();
        var scored = new List<(Job Job, double Score, int Gap, List<string> Matched, List<string> Missing)>();

        foreach (var job in allJobs)
        {
            var jSkills = ExtractSkills($"{job.Requirements} {job.JD}");
            if (jSkills.Count == 0) continue;
            var matched = cSkills.Intersect(jSkills, StringComparer.OrdinalIgnoreCase).ToList();
            var missing = jSkills.Except(cSkills, StringComparer.OrdinalIgnoreCase).ToList();
            var matchRate = (double)matched.Count / jSkills.Count * 100;
            scored.Add((job, matchRate, jSkills.Count - matched.Count, matched, missing));
        }

        var top5 = scored
            .OrderByDescending(s => s.Score)
            .Take(5)
            .Select(s => new CareerRecommendation
            {
                JobId = s.Job.JobId,
                JobTitle = s.Job.Title,
                MatchRate = Math.Round(s.Score, 1),
                SkillGapCount = s.Gap,
                Department = s.Job.Dept,
                Location = s.Job.Location,
                SalaryRange = s.Job.SalaryMin.HasValue && s.Job.SalaryMax.HasValue
                    ? $"{s.Job.SalaryMin}K-{s.Job.SalaryMax}K" : null,
                MatchedSkills = s.Matched,
                MissingSkills = s.Missing
            }).ToList();

        // ═══ AI 批量生成推荐理由（一次调用） ═══
        if (top5.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"候选人技能: {string.Join("、", cSkills)}");
            sb.AppendLine($"候选人学历: {candidate.Education ?? "未知"}");
            sb.AppendLine($"工作年限: {candidate.WorkYears?.ToString() ?? "未知"}年");
            sb.AppendLine();
            for (int i = 0; i < top5.Count; i++)
            {
                var r = top5[i];
                sb.AppendLine($"[岗位{i+1}] {r.JobTitle} ({r.Department}·{r.Location})");
                sb.AppendLine($"  匹配率: {r.MatchRate}%");
                sb.AppendLine($"  已有技能: {string.Join(", ", r.MatchedSkills)}");
                sb.AppendLine($"  缺失技能: {string.Join(", ", r.MissingSkills)}");
                sb.AppendLine();
            }

            var aiPrompt = $@"你是智能招聘助手。根据候选人技能和岗位匹配情况，为每个推荐岗位生成一句20-35字的个性化推荐理由。
要求：
- 每条理由说明为什么这个岗位适合该候选人
- 结合候选人的实际技能和岗位需求
- 语气温暖、鼓励，有说服力
- 每条不超过35字

返回纯JSON数组: [""理由1"", ""理由2"", ...]，只返回JSON不做其他输出。
每项对应上述[岗位1]...[岗位{top5.Count}]的顺序。

{sb}";

            var aiResult = await SafeCallAI(aiPrompt);
            if (!string.IsNullOrEmpty(aiResult))
            {
                try
                {
                    var cleaned = aiResult.Trim();
                    if (cleaned.StartsWith("```json")) cleaned = cleaned[7..];
                    else if (cleaned.StartsWith("```")) cleaned = cleaned[3..];
                    if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];
                    cleaned = cleaned.Trim();
                    var reasons = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(cleaned);
                    if (reasons != null)
                    {
                        for (int i = 0; i < Math.Min(reasons.Count, top5.Count); i++)
                            top5[i].AIReason = reasons[i];
                    }
                }
                catch { /* AI 解析失败，降级无理由 */ }
            }
        }

        return new CareerRecommendations
        {
            CandidateName = candidate.RealName,
            CurrentSkills = cSkills,
            Recommendations = top5
        };
    }

    // ═══════════════════════════════════════════
    // 2. 简历竞争力分析
    // ═══════════════════════════════════════════

    /// <summary>分析候选人在投递岗位上的竞争力排名</summary>
    public async Task<CompetitivenessReport> AnalyzeCompetitivenessAsync(int deliveryId)
    {
        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.DeliveryId == deliveryId);
        if (delivery == null) throw new Exception("投递记录不存在");

        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.CandidateId == delivery.CandidateId);
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == delivery.JobId);
        if (candidate == null || job == null) throw new Exception("候选人或岗位不存在");

        var skillsText = await GetCandidateSkillsTextAsync(delivery.CandidateId);
        var cSkills = ExtractSkills(skillsText);
        var jSkills = ExtractSkills($"{job.Requirements} {job.JD}");

        // 计算匹配度
        var matched = cSkills.Intersect(jSkills, StringComparer.OrdinalIgnoreCase).Count();
        var matchRate = jSkills.Count > 0 ? Math.Round((double)matched / jSkills.Count * 100, 1) : 0;

        // 获取同岗位其他投递者数量
        var competitors = await _db.Deliveries
            .CountAsync(d => d.JobId == delivery.JobId && d.DeliveryId != deliveryId);

        // 尝试估计排名（基于已有简历技能匹配）
        var rankEstimate = competitors > 0
            ? Math.Max(1, (int)(competitors * (1 - matchRate / 100)) + 1) : 1;

        // 技能深度分析
        var matchDetails = new List<SkillMatchDetail>();
        foreach (var js in jSkills.Take(10))
        {
            var has = cSkills.Any(cs => string.Equals(cs, js, StringComparison.OrdinalIgnoreCase));
            matchDetails.Add(new SkillMatchDetail
            {
                Skill = js,
                Has = has,
                Level = has ? "掌握" : "待学习",
                IsCore = jSkills.Take(5).Contains(js)
            });
        }

        return new CompetitivenessReport
        {
            CandidateName = candidate.RealName,
            JobTitle = job.Title,
            MatchRate = matchRate,
            TotalCompetitors = competitors,
            EstimatedRank = rankEstimate,
            Percentile = competitors > 0
                ? Math.Round((1 - (double)rankEstimate / competitors) * 100, 1) : 100,
            SkillMatchDetails = matchDetails,
            Strengths = matchDetails.Where(m => m.Has).Select(m => m.Skill).ToList(),
            Weaknesses = matchDetails.Where(m => !m.Has).Select(m => m.Skill).ToList()
        };
    }

    // ═══════════════════════════════════════════
    // 3. 岗位匹配透明度
    // ═══════════════════════════════════════════

    /// <summary>候选人在浏览岗位时即时看到匹配度（无需先投递）</summary>
    public async Task<TransparentMatchResult> GetTransparentMatchAsync(int candidateId, int jobId)
    {
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.CandidateId == candidateId);
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (candidate == null || job == null) throw new Exception("候选人或岗位不存在");

        var skillsText = await GetCandidateSkillsTextAsync(candidateId);
        var cSkills = ExtractSkills(skillsText);
        var jSkills = ExtractSkills($"{job.Requirements} {job.JD}");

        var matched = cSkills.Intersect(jSkills, StringComparer.OrdinalIgnoreCase).ToList();
        var missing = jSkills.Except(cSkills, StringComparer.OrdinalIgnoreCase).ToList();

        // 分维度计算
        var totalRequired = Math.Max(jSkills.Count, 1);
        var skillScore = Math.Round((double)matched.Count / totalRequired * 100, 1);

        // 经验匹配：工作时间 vs 岗位年限要求
        var workYears = candidate.WorkYears.GetValueOrDefault();
        var expScore = workYears switch { >= 5 => 90, >= 3 => 75, >= 2 => 60, _ => 40 };

        // 学历匹配
        var eduScore = !string.IsNullOrEmpty(candidate.Education) &&
            (candidate.Education.Contains("本科") || candidate.Education.Contains("硕士"))
            ? 80 : 50;

        var overall = Math.Round(skillScore * 0.5 + expScore * 0.2 + eduScore * 0.3, 1);

        return new TransparentMatchResult
        {
            CandidateName = candidate.RealName,
            JobTitle = job.Title,
            OverallScore = overall,
            SkillScore = skillScore,
            ExperienceScore = expScore,
            EducationScore = eduScore,
            MatchedSkills = matched,
            MissingSkills = missing,
            Recommendation = overall >= 80 ? "🌟 高度匹配，强烈建议投递"
                : overall >= 60 ? "👍 比较匹配，可以投递"
                : overall >= 40 ? "📚 有差距，建议补足技能后投递"
                : "🔄 差距较大，可考虑其他岗位"
        };
    }

    /// <summary>批量获取所有活跃岗位的匹配度（用于岗位列表）</summary>
    public async Task<List<TransparentMatchResult>> GetBatchMatchAsync(int candidateId, List<int> jobIds)
    {
        var results = new List<TransparentMatchResult>();
        foreach (var jid in jobIds.Take(10))
        {
            try { results.Add(await GetTransparentMatchAsync(candidateId, jid)); }
            catch { }
        }
        return results;
    }

    // ═══════════════════════════════════════════
    // 辅助
    // ═══════════════════════════════════════════

    /// <summary>从候选人所有可用数据源提取技能关键词</summary>
    private async Task<string> GetCandidateSkillsTextAsync(int candidateId)
    {
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.CandidateId == candidateId)
                     ?? await _db.Candidates.FirstOrDefaultAsync(c => c.UserId == candidateId);
        if (candidate == null) return "";

        var parts = new List<string>();

        // 1. 在线简历（手动填写，优先级最高）
        if (!string.IsNullOrWhiteSpace(candidate.ResumeContent))
            parts.Add(candidate.ResumeContent);

        // 2. AI 简历解析的技能标签
        var analysis = await _db.AIResumeAnalyses
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(a => a.CandidateId == candidateId);
        if (analysis?.SkillsTags != null)
            parts.Add(analysis.SkillsTags);
        if (analysis?.WorkExperience != null)
            parts.Add(analysis.WorkExperience);
        if (analysis?.Projects != null)
            parts.Add(analysis.Projects);

        // 3. 投递时上传的简历文本
        var delivery = await _db.Deliveries
            .OrderByDescending(d => d.DeliverTime)
            .FirstOrDefaultAsync(d => d.CandidateId == candidateId && d.ResumeText != null);
        if (delivery?.ResumeText != null)
            parts.Add(delivery.ResumeText);

        // 4. 学历 + 工作年限（兜底）
        if (!string.IsNullOrWhiteSpace(candidate.Education))
            parts.Add(candidate.Education);
        if (candidate.WorkYears.HasValue)
            parts.Add($"{candidate.WorkYears}年工作经验");

        return string.Join("\n", parts);
    }

    private static HashSet<string> ExtractSkills(string text) =>
        AccuracyTestData.KnownSkills
            .Where(s => text.Contains(s, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static int EstimateWeeks(string s) => s switch
    {
        "Git" or "SQL" or "HTML" or "CSS" or "Linux" => 2, "Docker" or "Redis" or "MongoDB" or "Nginx" => 4,
        "Python" or "JavaScript" or "TypeScript" or "Vue" or "React" => 6,
        "Java" or "Go" or "Kubernetes" or "微服务" or "分布式" => 8,
        "机器学习" or "深度学习" or "NLP" or "大模型" or "PyTorch" => 10, _ => 4
    };

    private static List<string> GetPrerequisites(string skill) => skill switch
    {
        "Kubernetes" => new() { "Docker", "Linux" }, "微服务" => new() { "Java", "Spring Boot" },
        "分布式" => new() { "Java", "网络协议" }, "大模型" => new() { "Python", "机器学习" },
        "PyTorch" => new() { "Python", "NumPy" }, "Spark" => new() { "Python", "SQL" },
        _ => new()
    };

    private async Task<string?> SafeCallAI(string prompt)
    {
        try { var t = _ai.ChatAsync(prompt); return await Task.WhenAny(t, Task.Delay(8000)) == t ? await t : null; }
        catch { return null; }
    }
}

// ═══════════════════════════════════════════
// 数据模型
// ═══════════════════════════════════════════

public class CareerPathResult
{
    public string CandidateName { get; set; } = "";
    public string TargetJob { get; set; } = "";
    public HashSet<string> CurrentSkills { get; set; } = new();
    public HashSet<string> TargetSkills { get; set; } = new();
    public List<string> MatchedSkills { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public List<CareerPathStep> Steps { get; set; } = new();
    public double CurrentMatchRate { get; set; }
    public int LearningWeeksTotal { get; set; }
    public double ProjectedMatchRate { get; set; }
    public string? AIAdvice { get; set; }
}

public class CareerPathStep
{
    public string Skill { get; set; } = "";
    public string Priority { get; set; } = "";
    public int EstimatedWeeks { get; set; }
    public string? LearningSuggestion { get; set; }
    public List<string> Prerequisites { get; set; } = new();
}

public class CareerRecommendations
{
    public string CandidateName { get; set; } = "";
    public HashSet<string> CurrentSkills { get; set; } = new();
    public List<CareerRecommendation> Recommendations { get; set; } = new();
}

public class CareerRecommendation
{
    public int JobId { get; set; }
    public string JobTitle { get; set; } = "";
    public double MatchRate { get; set; }
    public int SkillGapCount { get; set; }
    public string Department { get; set; } = "";
    public string Location { get; set; } = "";
    public string? SalaryRange { get; set; }
    public List<string> MatchedSkills { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public string? AIReason { get; set; }
}

public class CompetitivenessReport
{
    public string CandidateName { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public double MatchRate { get; set; }
    public int TotalCompetitors { get; set; }
    public int EstimatedRank { get; set; }
    public double Percentile { get; set; }
    public List<SkillMatchDetail> SkillMatchDetails { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
}

public class SkillMatchDetail
{
    public string Skill { get; set; } = "";
    public bool Has { get; set; }
    public string Level { get; set; } = "";
    public bool IsCore { get; set; }
}

public class TransparentMatchResult
{
    public string CandidateName { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public double OverallScore { get; set; }
    public double SkillScore { get; set; }
    public double ExperienceScore { get; set; }
    public double EducationScore { get; set; }
    public List<string> MatchedSkills { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public string Recommendation { get; set; } = "";
}
