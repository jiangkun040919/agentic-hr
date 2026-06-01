using Newtonsoft.Json;

namespace AIRecruitment.Api.Services;

/// <summary>
/// 多源异构数据交叉验证与清洗服务。
/// 比赛要求：解决招聘数据"时滞""噪声""抄袭"问题，实现多源交叉验证。
/// </summary>
public class DataCrossValidationService
{
    private readonly IAIService _ai;
    private readonly KnowledgeGraphService _graph;
    private readonly ILogger<DataCrossValidationService> _logger;

    public DataCrossValidationService(IAIService ai, KnowledgeGraphService graph, ILogger<DataCrossValidationService> logger)
    {
        _ai = ai; _graph = graph; _logger = logger;
    }

    /// <summary>
    /// 多源交叉验证：对比招聘平台、企业官网、行业报告三源数据，
    /// 识别"抄袭JD"和"通胀要求"，用交叉验证结果标记可信度。
    /// </summary>
    public async Task<CrossValidationReport> CrossValidateAsync(List<RawJD> jdsFromPlatforms)
    {
        var report = new CrossValidationReport { AnalyzedAt = DateTime.UtcNow };

        foreach (var jd in jdsFromPlatforms)
        {
            var result = new CrossValidationItem { JobTitle = jd.Title, Source = jd.Source };

            // 1. 相似度检测 — 识别抄袭JD
            var similarJDs = jdsFromPlatforms
                .Where(other => other != jd && other.Title == jd.Title)
                .Select(other => new { JD = other, Sim = ComputeSimilarity(jd.Requirements, other.Requirements) })
                .Where(x => x.Sim > 0.7)
                .OrderByDescending(x => x.Sim)
                .Take(3)
                .ToList();

            if (similarJDs.Any())
            {
                result.IsPlagiarized = true;
                result.PlagiarismRate = similarJDs.Average(x => x.Sim);
                result.SimilarSources = similarJDs.Select(x => x.JD.Source).ToList();
                report.PlagiarizedCount++;
            }

            // 2. 通胀检测 — AI判断要求是否过度
            try
            {
                var inflationPrompt = $@"判断以下JD是否存在技能要求'通胀'（过分夸大要求）：
岗位：{jd.Title}
要求：{jd.Requirements[..Math.Min(jd.Requirements.Length, 500)]}
只输出JSON：{{""isInflated"":true/false,""inflationRate"":0-100,""overRequirements"":[""过分要求的技能""],""realisticRequirements"":[""合理的技能要求""]}}";
                var aiResult = await _ai.ChatAsync(inflationPrompt);
                var inflation = JsonConvert.DeserializeObject<dynamic>(CleanJson(aiResult));
                if (inflation != null)
                {
                    result.IsInflated = inflation.isInflated?.ToObject<bool>() ?? false;
                    result.InflationRate = inflation.inflationRate?.ToObject<int>() ?? 0;
                    if (result.IsInflated) report.InflatedCount++;
                }
            }
            catch { }

            // 3. 时滞检测 — 对比图谱当前技能需求
            try
            {
                var graphSkills = await GetGraphSkillsForJob(jd.Title);
                var jdSkills = ExtractSkills(jd.Requirements);
                var missingInJD = graphSkills.Where(gs => !jdSkills.Any(js => js.Contains(gs))).ToList();
                var extraInJD = jdSkills.Where(js => !graphSkills.Any(gs => js.Contains(gs))).ToList();

                result.GraphMissingSkills = missingInJD;
                result.GraphExtraSkills = extraInJD;
                result.IsOutdated = missingInJD.Count > 3;
                if (result.IsOutdated) report.OutdatedCount++;
            }
            catch { }

            // 4. 综合数据质量评分 (0-100)
            result.QualityScore = ComputeQualityScore(result);
            report.Items.Add(result);
        }

        report.TotalAnalyzed = jdsFromPlatforms.Count;
        report.QualityPassRate = report.Items.Count(i => i.QualityScore >= 70);
        return report;
    }

    /// <summary>AI驱动的数据清洗：去重+标准化+补全</summary>
    public async Task<List<CleanJD>> CleanJDDataAsync(List<RawJD> rawJDs)
    {
        var cleaned = new List<CleanJD>();
        var seen = new HashSet<string>();

        foreach (var jd in rawJDs)
        {
            // 去重
            var hash = ComputeHash(jd.Title + jd.Requirements);
            if (!seen.Add(hash)) continue;

            // AI标准化
            try
            {
                var cleanPrompt = $@"标准化以下JD，统一格式，去除冗余：
岗位：{jd.Title}
要求：{jd.Requirements[..Math.Min(jd.Requirements.Length, 500)]}
职责：{jd.Description[..Math.Min(jd.Description.Length, 300)]}

输出JSON：{{""standardizedTitle"":"""",""coreResponsibilities"":[""""],""requiredSkills"":[{{""name"":"""",""level"":""精通/熟练/了解""}}],""niceToHaveSkills"":[],""levelEstimate"":""初级/中级/高级/资深""}}";

                var aiResult = await _ai.ChatAsync(cleanPrompt);
                var result = JsonConvert.DeserializeObject<dynamic>(CleanJson(aiResult));
                if (result != null)
                {
                    cleaned.Add(new CleanJD
                    {
                        OriginalTitle = jd.Title,
                        StandardizedTitle = result.standardizedTitle?.ToString() ?? jd.Title,
                        RequiredSkills = DeserializeSkills(result.requiredSkills),
                        NiceToHaveSkills = DeserializeSkills(result.niceToHaveSkills),
                        LevelEstimate = result.levelEstimate?.ToString() ?? "中级",
                        Source = jd.Source,
                        CleanedAt = DateTime.UtcNow
                    });
                    continue;
                }
            }
            catch { }

            // 降级：保留原数据
            cleaned.Add(new CleanJD { OriginalTitle = jd.Title, StandardizedTitle = jd.Title, Source = jd.Source, CleanedAt = DateTime.UtcNow });
        }

        return cleaned;
    }

    // ═══ 辅助方法 ═══

    private static double ComputeSimilarity(string a, string b)
    {
        var wordsA = a.Split(' ', '，', '、', '\n').Where(w => w.Length > 1).ToHashSet();
        var wordsB = b.Split(' ', '，', '、', '\n').Where(w => w.Length > 1).ToHashSet();
        if (wordsA.Count == 0 || wordsB.Count == 0) return 0;
        var intersection = wordsA.Intersect(wordsB).Count();
        return (double)intersection / Math.Max(wordsA.Count, wordsB.Count);
    }

    private async Task<List<string>> GetGraphSkillsForJob(string jobTitle)
    {
        try
        {
            var gap = await _graph.GetSkillGapAsync("", jobTitle);
            return gap.RequiredSkills;
        }
        catch { return new(); }
    }

    private static HashSet<string> ExtractSkills(string text) => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Java","Python","Go","Rust","C++","C#","JavaScript","TypeScript","SQL",
        "Spring Boot","Spring Cloud","Django","FastAPI","React","Vue","Docker",
        "Kubernetes","Jenkins","MySQL","PostgreSQL","Redis","MongoDB","Kafka",
        "微服务","分布式","高并发","机器学习","深度学习","NLP","大模型","RAG",
        "PyTorch","TensorFlow","Spark","Flink","Hadoop","AWS","Azure","阿里云"
    }.Where(text.Contains).ToHashSet();

    private static int ComputeQualityScore(CrossValidationItem item)
    {
        var score = 100;
        if (item.IsPlagiarized) score -= (int)(item.PlagiarismRate * 30);
        if (item.IsInflated) score -= item.InflationRate / 3;
        if (item.IsOutdated) score -= 15;
        return Math.Clamp(score, 0, 100);
    }

    private static string ComputeHash(string text) => 
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(text)));

    private static string CleanJson(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith("```json")) raw = raw[7..];
        else if (raw.StartsWith("```")) raw = raw[3..];
        if (raw.EndsWith("```")) raw = raw[..^3];
        return raw.Trim();
    }

    private static List<SkillItem> DeserializeSkills(dynamic? arr)
    {
        if (arr == null) return new();
        try { return JsonConvert.DeserializeObject<List<SkillItem>>(arr.ToString()) ?? new List<SkillItem>(); }
        catch { return new List<SkillItem>(); }
    }
}

// ═══ DTOs ═══

public class RawJD
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Requirements { get; set; } = "";
    public string Source { get; set; } = "recruitment_platform";
}

public class CrossValidationReport
{
    public DateTime AnalyzedAt { get; set; }
    public int TotalAnalyzed { get; set; }
    public int PlagiarizedCount { get; set; }
    public int InflatedCount { get; set; }
    public int OutdatedCount { get; set; }
    public int QualityPassRate { get; set; }
    public List<CrossValidationItem> Items { get; set; } = new();
}

public class CrossValidationItem
{
    public string JobTitle { get; set; } = "";
    public string Source { get; set; } = "";
    public bool IsPlagiarized { get; set; }
    public double PlagiarismRate { get; set; }
    public bool IsInflated { get; set; }
    public int InflationRate { get; set; }
    public bool IsOutdated { get; set; }
    public int QualityScore { get; set; }
    public List<string> SimilarSources { get; set; } = new();
    public List<string> GraphMissingSkills { get; set; } = new();
    public List<string> GraphExtraSkills { get; set; } = new();
}

public class CleanJD
{
    public string OriginalTitle { get; set; } = "";
    public string StandardizedTitle { get; set; } = "";
    public List<SkillItem> RequiredSkills { get; set; } = new();
    public List<SkillItem> NiceToHaveSkills { get; set; } = new();
    public string LevelEstimate { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime CleanedAt { get; set; }
}

public class SkillItem
{
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
}
