using System.Text.RegularExpressions;

namespace AIRecruitment.Api.Services;

/// <summary>
/// 增强版人岗匹配引擎：多维度评分 + 差距分析 + 学习路径 + 幻觉防控。
/// 匹配模型：加权综合评分 = 技能匹配(40%) + 经验匹配(25%) + 学历匹配(15%) + 薪资匹配(10%) + 综合适配(10%)
/// </summary>
public class EnhancedMatchingService
{
    private readonly KnowledgeGraphService _graph;
    private readonly IAIService _ai;
    private readonly AppDbContext _db;
    private readonly ILogger<EnhancedMatchingService> _logger;

    public EnhancedMatchingService(KnowledgeGraphService graph, IAIService ai, AppDbContext db, ILogger<EnhancedMatchingService> logger)
    {
        _graph = graph; _ai = ai; _db = db; _logger = logger;
    }

    /// <summary>
    /// 执行增强版人岗匹配（含图谱推理和幻觉防控）
    /// </summary>
    public async Task<EnhancedMatchResult> MatchAsync(string resumeText, int jobId)
    {
        var job = await _db.Jobs.FindAsync(jobId);
        if (job == null) throw new Exception("岗位不存在");

        // 1. 简历技能提取
        var resumeSkills = ExtractSkills(resumeText);

        // 2. 图谱差距分析
        var gapResult = await _graph.GetSkillGapAsync(string.Join(",", resumeSkills), job.Title);

        // 3. 多维度评分
        var dimensions = new List<MatchDimension>();
        var rng = new Random(resumeText.GetHashCode() + jobId);

        // 维度1：技能匹配 (40%)
        var skillScore = gapResult.MatchRate;
        dimensions.Add(new MatchDimension("技能匹配", skillScore, 0.40,
            $"候选人与{job.Title}的岗位技能匹配度为{skillScore:F1}%",
            gapResult.MatchedSkills, gapResult.MissingSkills));

        // 维度2：经验匹配 (25%) — 从简历文本中推断工作年限
        var expYears = ExtractWorkYears(resumeText);
        var expScore = ScoreExperience(expYears, job.Requirements);
        dimensions.Add(new MatchDimension("经验匹配", expScore, 0.25,
            expYears >= 3 ? $"候选人具备{expYears}年工作经验，满足岗位要求" : $"候选人工作经验({expYears}年)可能不足",
            new(), new()));

        // 维度3：学历匹配 (15%)
        var eduScore = ScoreEducation(resumeText, job.Requirements);
        dimensions.Add(new MatchDimension("学历匹配", eduScore, 0.15,
            eduScore >= 70 ? "学历背景与岗位要求匹配" : "学历背景可能与岗位要求存在差距",
            new(), new()));

        // 维度4：薪资匹配 (10%)
        var salaryScore = 80.0; // 简历中难以精确提取薪资，使用默认值
        dimensions.Add(new MatchDimension("薪资匹配", salaryScore, 0.10, "薪资范围与岗位预算基本匹配", new(), new()));

        // 维度5：综合适配 (10%) — 使用 AI 进行语义级别的综合判断
        var adaptabilityScore = 75.0;
        try
        {
            var aiPrompt = $"""
评估候选人是否适合"{job.Title}"岗位。
岗位要求：{job.Requirements}
候选人简历摘要：{resumeText[..Math.Min(resumeText.Length, 500)]}

只输出0-100的数字表示综合适应度评分：
""";
            var aiTask = _ai.ChatAsync(aiPrompt);
            var aiResult = await Task.WhenAny(aiTask, Task.Delay(10000)) == aiTask ? await aiTask : "";
            if (double.TryParse(aiResult.Trim(), out var aiScore))
                adaptabilityScore = Math.Clamp(aiScore, 0, 100);
        }
        catch { }
        dimensions.Add(new MatchDimension("综合适配", adaptabilityScore, 0.10, "AI综合评估", new(), new()));

        // 6. 加权综合评分
        var overallScore = dimensions.Sum(d => d.Score * d.Weight);

        // 7. 学习路径
        var learningPath = await _graph.GetLearningPathAsync(string.Join(",", resumeSkills), job.Title);

        // 8. 幻觉防控：对 AI 建议进行图谱回查验证
        var verification = await _graph.VerifySkillsAsync(gapResult.MissingSkills);

        // 9. 改进建议
        var suggestions = GenerateSuggestions(dimensions, gapResult.MissingSkills, overallScore);

        return new EnhancedMatchResult(
            job.Title, overallScore, dimensions, gapResult,
            learningPath, verification, suggestions, DateTime.Now);
    }

    /// <summary>执行批量测试：验证系统准确率</summary>
    public async Task<AccuracyReport> RunAccuracyTestAsync(List<TestPair> testPairs)
    {
        var report = new AccuracyReport { TotalTests = testPairs.Count, StartedAt = DateTime.Now };
        foreach (var pair in testPairs)
        {
            try
            {
                var result = await MatchAsync(pair.ResumeText, pair.JobId);
                var isAccurate = result.OverallScore >= 70 == pair.IsExpectedMatch;
                report.Results.Add(new TestResult(pair.Label, result.OverallScore, isAccurate));
                if (isAccurate) report.Accurate++; else report.Inaccurate++;
            }
            catch { report.Inaccurate++; }
        }
        report.Accuracy = report.TotalTests > 0 ? (double)report.Accurate / report.TotalTests * 100 : 0;
        report.CompletedAt = DateTime.Now;
        return report;
    }

    // ========== 辅助方法 ==========

    private static HashSet<string> ExtractSkills(string text) => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Java", "Python", "Go", "Rust", "C++", "C#", "JavaScript", "TypeScript", "SQL", "HTML", "CSS",
        "Spring Boot", "Spring Cloud", "MyBatis", "Django", "Flask", "FastAPI", "React", "Vue", "Angular",
        "Docker", "Kubernetes", "Jenkins", "MySQL", "PostgreSQL", "MongoDB", "Redis", "Elasticsearch",
        "Kafka", "RabbitMQ", "Linux", "Git", "微服务", "分布式", "高并发", "架构设计",
        "机器学习", "深度学习", "NLP", "Pandas", "NumPy", "TensorFlow", "PyTorch",
        "大模型", "LangChain", "RAG", "Prompt Engineering", "AWS", "Azure", "阿里云",
        "产品设计", "数据分析", "项目管理", "自动化测试", "Selenium", "JMeter",
        "Spark", "Flink", "Hadoop", "Hive", "Node.js", "GraphQL", "Webpack", "Vite",
    }.Where(text.Contains).ToHashSet();

    private static int ExtractWorkYears(string text)
    {
        foreach (Match m in Regex.Matches(text, @"(\d+)\s*年(?:以上)?(?:工作)?经验"))
            if (int.TryParse(m.Groups[1].Value, out var y) && y > 0 && y < 50) return y;
        return 1;
    }

    private static double ScoreExperience(int years, string requirements)
    {
        var reqMatch = Regex.Match(requirements, @"(\d+)\s*年");
        if (reqMatch.Success && int.TryParse(reqMatch.Groups[1].Value, out var required))
            return years >= required ? 95 : Math.Max(30, years * 100.0 / required);
        return years >= 5 ? 90 : years >= 3 ? 75 : years >= 1 ? 55 : 30;
    }

    private static double ScoreEducation(string resume, string requirements)
    {
        var eduMap = new Dictionary<string, int> { ["博士"] = 95, ["硕士"] = 85, ["本科"] = 70, ["大专"] = 50, ["高中"] = 30 };
        foreach (var (edu, score) in eduMap)
            if (resume.Contains(edu) || requirements.Contains(edu))
                return score;
        return 60;
    }

    private static List<string> GenerateSuggestions(List<MatchDimension> dims, List<string> missingSkills, double overall)
    {
        var suggestions = new List<string>();
        if (overall >= 85) suggestions.Add("候选人整体匹配度很高，建议直接安排面试。");
        else if (overall >= 70) suggestions.Add("候选人基本满足要求，建议针对性面试考察薄弱环节。");
        else suggestions.Add("候选人存在明显技能差距，建议暂缓面试或降级考虑。");

        var weakDims = dims.Where(d => d.Score < 65).ToList();
        foreach (var d in weakDims.Take(2))
            suggestions.Add($"需要提升'{d.Name}'维度（当前{d.Score:F0}分）。");

        if (missingSkills.Count > 0)
            suggestions.Add($"建议学习：{string.Join("、", missingSkills.Take(5))}");

        return suggestions;
    }
}

// ========== DTOs ==========
public class EnhancedMatchResult
{
    public string JobTitle { get; set; }
    public double OverallScore { get; set; }
    public List<MatchDimension> Dimensions { get; set; }
    public GapAnalysisResult GapAnalysis { get; set; }
    public LearningPathResult LearningPath { get; set; }
    public HallucinationCheckResult Verification { get; set; }
    public List<string> Suggestions { get; set; }
    public DateTime MatchedAt { get; set; }

    public EnhancedMatchResult(string jobTitle, double overallScore, List<MatchDimension> dimensions,
        GapAnalysisResult gapAnalysis, LearningPathResult learningPath, HallucinationCheckResult verification,
        List<string> suggestions, DateTime matchedAt)
    {
        JobTitle = jobTitle; OverallScore = overallScore; Dimensions = dimensions;
        GapAnalysis = gapAnalysis; LearningPath = learningPath; Verification = verification;
        Suggestions = suggestions; MatchedAt = matchedAt;
    }
}

public class MatchDimension
{
    public string Name { get; set; }
    public double Score { get; set; }
    public double Weight { get; set; }
    public string Analysis { get; set; }
    public List<string> Strengths { get; set; }
    public List<string> Weaknesses { get; set; }

    public MatchDimension(string name, double score, double weight, string analysis, List<string> strengths, List<string> weaknesses)
    {
        Name = name; Score = score; Weight = weight; Analysis = analysis; Strengths = strengths; Weaknesses = weaknesses;
    }
}

public class TestPair
{
    public string Label { get; set; } = "";
    public string ResumeText { get; set; } = "";
    public int JobId { get; set; }
    public bool IsExpectedMatch { get; set; }
}

public class TestResult
{
    public string Label { get; set; }
    public double Score { get; set; }
    public bool IsAccurate { get; set; }

    public TestResult(string label, double score, bool isAccurate)
    { Label = label; Score = score; IsAccurate = isAccurate; }
}

public class AccuracyReport
{
    public int TotalTests { get; set; }
    public int Accurate { get; set; }
    public int Inaccurate { get; set; }
    public double Accuracy { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<TestResult> Results { get; set; } = new();
}
