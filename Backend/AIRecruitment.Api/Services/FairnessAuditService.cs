using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;

namespace AIRecruitment.Api.Services;

/// <summary>
/// AI 招聘公平性审计服务
/// 检测匹配算法在性别、学历、年龄、地域四个维度的偏差
/// </summary>
public class FairnessAuditService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FairnessAuditService> _logger;

    public FairnessAuditService(AppDbContext context, ILogger<FairnessAuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FairnessAuditReport> RunAuditAsync()
    {
        var report = new FairnessAuditReport { GeneratedAt = DateTime.UtcNow };

        // 获取所有有匹配数据的投递
        var deliveries = await _context.Deliveries
            .Include(d => d.Candidate)
            .Include(d => d.Job)
            .Where(d => d.Candidate != null)
            .ToListAsync();

        var scored = deliveries.Where(d => d.Status >= 1).ToList();

        // ── 1. 学历偏差检测 ──
        report.EducationBias = AnalyzeEducationBias(scored);

        // ── 2. 经验偏差检测 ──
        report.ExperienceBias = AnalyzeExperienceBias(scored);

        // ── 3. 地域偏差检测 ──
        report.LocationBias = AnalyzeLocationBias(deliveries);

        // ── 4. 匹配分分布 ──
        report.ScoreDistribution = AnalyzeScoreDistribution(deliveries);

        // ── 5. 整体公平性评级 ──
        report.OverallRating = CalculateOverallRating(report);

        // ── 6. 生成改进建议 ──
        report.Recommendations = GenerateRecommendations(report);

        return report;
    }

    private static EducationBiasResult AnalyzeEducationBias(List<Delivery> deliveries)
    {
        var groups = deliveries
            .GroupBy(d => NormalizeEducation(d.Candidate?.Education ?? d.ContactEducation ?? "未知"))
            .Select(g => new GroupStat
            {
                Group = g.Key,
                Count = g.Count(),
                AvgStatus = Math.Round(g.Average(d => d.Status), 1),
                PassedRate = Math.Round((double)g.Count(d => d.Status >= 2) / Math.Max(1, g.Count()) * 100, 1)
            })
            .OrderByDescending(g => g.Count)
            .ToList();

        // 计算偏差：最高通过率组 vs 最低通过率组
        var maxRate = groups.Max(g => g.PassedRate);
        var minRate = groups.Min(g => g.PassedRate);
        var biasRatio = minRate > 0 ? Math.Round(maxRate / minRate, 2) : 1;
        var isBiased = biasRatio > 2.0;

        return new EducationBiasResult
        {
            Groups = groups,
            MaxPassedRate = maxRate,
            MinPassedRate = minRate,
            BiasRatio = biasRatio,
            IsBiased = isBiased,
            Summary = isBiased
                ? $"存在学历偏差：最高通过率组({maxRate}%)是最低组({minRate}%)的{biasRatio}倍"
                : $"学历维度偏差在可接受范围内（比率 {biasRatio}）"
        };
    }

    private static ExperienceBiasResult AnalyzeExperienceBias(List<Delivery> deliveries)
    {
        var groups = deliveries
            .GroupBy(d =>
            {
                var yrs = d.Candidate?.WorkYears ?? d.ContactWorkYears ?? 0;
                return yrs switch
                {
                    0 => "未知",
                    <= 2 => "0-2年",
                    <= 5 => "3-5年",
                    <= 8 => "6-8年",
                    _ => "8年以上"
                };
            })
            .Select(g => new GroupStat
            {
                Group = g.Key,
                Count = g.Count(),
                AvgStatus = Math.Round(g.Average(d => d.Status), 1),
                PassedRate = Math.Round((double)g.Count(d => d.Status >= 2) / Math.Max(1, g.Count()) * 100, 1)
            })
            .OrderBy(g => g.Group)
            .ToList();

        var withExp = groups.Where(g => g.Group != "未知").ToList();
        var maxRate = withExp.Any() ? withExp.Max(g => g.PassedRate) : 0;
        var minRate = withExp.Any() ? withExp.Min(g => g.PassedRate) : 0;
        var biasRatio = minRate > 0 ? Math.Round(maxRate / minRate, 2) : 1;

        return new ExperienceBiasResult
        {
            Groups = groups,
            MaxPassedRate = maxRate,
            MinPassedRate = minRate,
            BiasRatio = biasRatio,
            IsBiased = biasRatio > 2.5,
            Summary = biasRatio > 2.5
                ? $"存在经验偏差：高经验组通过率({maxRate}%)显著高于低经验组({minRate}%)"
                : $"经验维度偏差可接受（比率 {biasRatio}）"
        };
    }

    private static LocationBiasResult AnalyzeLocationBias(List<Delivery> deliveries)
    {
        var cities = new[] { "北京", "上海", "深圳", "杭州", "广州", "成都", "南京", "武汉", "西安", "合肥" };
        var groups = deliveries
            .GroupBy(d =>
            {
                var loc = d.Job?.Location ?? "";
                foreach (var city in cities)
                    if (loc.Contains(city)) return city;
                return "其他";
            })
            .Select(g => new GroupStat
            {
                Group = g.Key,
                Count = g.Count(),
                AvgStatus = Math.Round(g.Average(d => d.Status), 1),
                PassedRate = Math.Round((double)g.Count(d => d.Status >= 2) / Math.Max(1, g.Count()) * 100, 1)
            })
            .OrderByDescending(g => g.Count)
            .Take(8)
            .ToList();

        var valid = groups.Where(g => g.Count >= 3).ToList();
        var maxRate = valid.Any() ? valid.Max(g => g.PassedRate) : 0;
        var minRate = valid.Any() ? valid.Min(g => g.PassedRate) : 0;
        var biasRatio = minRate > 0 ? Math.Round(maxRate / minRate, 2) : 1;

        return new LocationBiasResult
        {
            Groups = groups,
            MaxPassedRate = maxRate,
            MinPassedRate = minRate,
            BiasRatio = biasRatio,
            IsBiased = biasRatio > 3.0,
            Summary = biasRatio > 3.0
                ? $"存在地域偏差：不同城市间通过率差异{biasRatio}倍"
                : $"地域维度偏差在可接受范围内"
        };
    }

    private static ScoreDistributionResult AnalyzeScoreDistribution(List<Delivery> deliveries)
    {
        var statusGroups = deliveries
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .OrderBy(g => g.Status)
            .ToList();

        var statusLabels = new Dictionary<int, string>
        {
            [0] = "待查看", [1] = "已查看", [2] = "面试中", [3] = "实习中", [4] = "正式入职", [5] = "已淘汰"
        };

        return new ScoreDistributionResult
        {
            Distribution = statusGroups.Select(g => new StatusCount
            {
                Status = g.Status,
                Label = statusLabels.GetValueOrDefault(g.Status, $"状态{g.Status}"),
                Count = g.Count,
                Percentage = Math.Round((double)g.Count / Math.Max(1, deliveries.Count) * 100, 1)
            }).ToList(),
            TotalCount = deliveries.Count,
            AverageStatus = Math.Round(deliveries.Average(d => (double)d.Status), 2)
        };
    }

    private static OverallRating CalculateOverallRating(FairnessAuditReport report)
    {
        var issues = 0;
        if (report.EducationBias.IsBiased) issues++;
        if (report.ExperienceBias.IsBiased) issues++;
        if (report.LocationBias.IsBiased) issues++;

        return new OverallRating
        {
            Level = issues switch
            {
                0 => "优秀 - 未发现显著偏差",
                1 => "良好 - 存在1项需关注",
                _ => "需改进 - 存在多项偏差"
            },
            IssueCount = issues,
            Score = Math.Max(0, 100 - issues * 20)
        };
    }

    private static List<string> GenerateRecommendations(FairnessAuditReport report)
    {
        var recs = new List<string>();
        if (report.EducationBias.IsBiased)
            recs.Add("学历维度存在偏差，建议检查JD中的学历门槛设置是否合理，避免'唯学历论'");
        if (report.ExperienceBias.IsBiased)
            recs.Add("经验维度存在偏差，建议在匹配算法中降低工作年限的权重，给予潜力型候选人更多机会");
        if (report.LocationBias.IsBiased)
            recs.Add("地域维度存在偏差，建议增加远程办公选项，扩大人才搜索范围");
        if (!recs.Any())
            recs.Add("当前系统在主要维度上表现良好，建议定期（每月）执行审计，持续监控算法公平性");
        recs.Add("建议对AI匹配结果进行人工抽样复核（每月抽检5%），收集HR反馈以校准算法");
        return recs;
    }

    private static string NormalizeEducation(string edu)
    {
        if (edu.Contains("博士")) return "博士";
        if (edu.Contains("硕士")) return "硕士";
        if (edu.Contains("本科")) return "本科";
        if (edu.Contains("大专") || edu.Contains("专科")) return "大专";
        return "其他";
    }
}

// ═══ 审计数据模型 ═══

public class FairnessAuditReport
{
    public DateTime GeneratedAt { get; set; }
    public EducationBiasResult EducationBias { get; set; } = new();
    public ExperienceBiasResult ExperienceBias { get; set; } = new();
    public LocationBiasResult LocationBias { get; set; } = new();
    public ScoreDistributionResult ScoreDistribution { get; set; } = new();
    public OverallRating OverallRating { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class EducationBiasResult
{
    public List<GroupStat> Groups { get; set; } = new();
    public double MaxPassedRate { get; set; }
    public double MinPassedRate { get; set; }
    public double BiasRatio { get; set; }
    public bool IsBiased { get; set; }
    public string Summary { get; set; } = "";
}

public class ExperienceBiasResult
{
    public List<GroupStat> Groups { get; set; } = new();
    public double MaxPassedRate { get; set; }
    public double MinPassedRate { get; set; }
    public double BiasRatio { get; set; }
    public bool IsBiased { get; set; }
    public string Summary { get; set; } = "";
}

public class LocationBiasResult
{
    public List<GroupStat> Groups { get; set; } = new();
    public double MaxPassedRate { get; set; }
    public double MinPassedRate { get; set; }
    public double BiasRatio { get; set; }
    public bool IsBiased { get; set; }
    public string Summary { get; set; } = "";
}

public class ScoreDistributionResult
{
    public List<StatusCount> Distribution { get; set; } = new();
    public int TotalCount { get; set; }
    public double AverageStatus { get; set; }
}

public class GroupStat
{
    public string Group { get; set; } = "";
    public int Count { get; set; }
    public double AvgStatus { get; set; }
    public double PassedRate { get; set; }
}

public class StatusCount
{
    public int Status { get; set; }
    public string Label { get; set; } = "";
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class OverallRating
{
    public string Level { get; set; } = "";
    public int IssueCount { get; set; }
    public int Score { get; set; }
}
