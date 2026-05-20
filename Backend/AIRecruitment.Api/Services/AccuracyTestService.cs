using System.Text.Json;

namespace AIRecruitment.Api.Services;

public interface IAccuracyTestService
{
    Task<AccuracyTestReport> RunAllTestsAsync(int count = 10);
    Task<AccuracyMetric> TestJDParseAsync(List<JDTestItem> testSet);
    Task<AccuracyMetric> TestResumeExtractionAsync(List<ResumeTestItem> testSet);
    Task<AccuracyMetric> TestMatchingAsync(List<MatchTestItem> testSet);
    Task<List<JDTestItem>> GenerateJDTestSetAsync(int count);
    Task<List<ResumeTestItem>> GenerateResumeTestSetAsync(int count);
    Task<List<MatchTestItem>> GenerateMatchTestSetAsync(int count);
}

public record AccuracyTestReport(
    double JdParseAccuracy,
    double ResumeExtractAccuracy,
    double MatchingAccuracy,
    AccuracyMetric JdDetail,
    AccuracyMetric ResumeDetail,
    AccuracyMetric MatchDetail,
    string Summary);

public record AccuracyMetric(
    double Accuracy,
    int TotalFields,
    int CorrectFields,
    List<AccuracyError> Errors);

public record AccuracyError(string ItemId, string Field, string Expected, string Actual);

// ── Test Item Types ──

public record JDTestItem(
    string Id,
    string RawText,
    JDGroundTruth GroundTruth);

public record JDGroundTruth(
    string Title,
    string Dept,
    string Location,
    string[] Skills,
    int? SalaryMin,
    int? SalaryMax);

public record ResumeTestItem(
    string Id,
    string RawText,
    ResumeGroundTruth GroundTruth);

public record ResumeGroundTruth(
    string Name,
    string Phone,
    string? Email,
    string? Education,
    int? WorkYears,
    string[] Skills,
    string[]? WorkExperience,
    string[]? Projects);

public record MatchTestItem(
    string Id,
    ResumeTestItem Resume,
    JDTestItem JD,
    bool ShouldMatch,
    double? ExpectedScore);

public class AccuracyTestService : IAccuracyTestService
{
    private readonly IAIService _ai;
    private readonly KnowledgeGraphService _graph;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccuracyTestService> _logger;

    private static readonly string[] ITDepts = { "技术部", "产品部", "数据部", "架构部" };
    private static readonly string[] Locations = { "深圳", "北京", "上海", "广州", "杭州" };
    private static readonly string[] EducationLevels = { "本科", "硕士", "博士", "大专" };
    private static readonly string[] JobPositions = {
        "Java后端", "前端开发", "Python开发", "DevOps", "数据分析师", "算法工程师",
        "产品经理", "测试工程师", "架构师", "全栈工程师", "iOS开发", "Android开发",
        "DBA", "安全工程师", "运维工程师", "AI工程师", "大数据工程师", "嵌入式开发",
        "Node.js后端", "Go开发", "C++开发", ".NET开发", "PHP开发",
    };

    public AccuracyTestService(IAIService ai, KnowledgeGraphService graph,
        IServiceScopeFactory scopeFactory, ILogger<AccuracyTestService> logger)
    { _ai = ai; _graph = graph; _scopeFactory = scopeFactory; _logger = logger; }

    // ═══ Main Test Runner ═══

    public async Task<AccuracyTestReport> RunAllTestsAsync(int count = 10)
    {
        _logger.LogInformation("开始全量准确率测试（基于真实AI提取）...");

        var jdSet = await GenerateJDTestSetAsync(count);
        var resumeSet = await GenerateResumeTestSetAsync(count);
        var matchSet = await GenerateMatchTestSetAsync(count);

        var jdMetric = await TestJDParseAsync(jdSet);
        var resumeMetric = await TestResumeExtractionAsync(resumeSet);
        var matchMetric = await TestMatchingAsync(matchSet);

        var report = new AccuracyTestReport(
            JdParseAccuracy: jdMetric.Accuracy,
            ResumeExtractAccuracy: resumeMetric.Accuracy,
            MatchingAccuracy: matchMetric.Accuracy,
            JdDetail: jdMetric,
            ResumeDetail: resumeMetric,
            MatchDetail: matchMetric,
            Summary: GenerateSummary(jdMetric, resumeMetric, matchMetric)
        );

        _logger.LogInformation("测试完成: JD={jd}%, Resume={res}%, Match={mat}%",
            jdMetric.Accuracy * 100, resumeMetric.Accuracy * 100, matchMetric.Accuracy * 100);

        return report;
    }

    // ═══ JD Parse Test: 真实调用AI提取后与ground truth对比 ═══

    public async Task<AccuracyMetric> TestJDParseAsync(List<JDTestItem> testSet)
    {
        var correct = 0; var total = 0; var errors = new List<AccuracyError>();

        foreach (var item in testSet)
        {
            try
            {
                var prompt = BuildJDParsePrompt(item.RawText);
                var aiResponse = await _ai.ChatAsync(prompt);
                var extracted = ParseJDExtraction(aiResponse);
                var gt = item.GroundTruth;

                // Title fuzzy match
                if (FuzzyMatch(extracted.Title, gt.Title))
                    correct++;
                else
                    errors.Add(new AccuracyError(item.Id, "Title", gt.Title, extracted.Title));
                total++;

                // Dept match
                if (FuzzyContains(gt.Dept, extracted.Dept))
                    correct++;
                else
                    errors.Add(new AccuracyError(item.Id, "Dept", gt.Dept, extracted.Dept));
                total++;

                // Location match
                if (FuzzyContains(gt.Location, extracted.Location))
                    correct++;
                else
                    errors.Add(new AccuracyError(item.Id, "Location", gt.Location, extracted.Location));
                total++;

                // Skills overlap (≥50%)
                if (gt.Skills.Length > 0 && extracted.Skills.Length > 0)
                {
                    var matched = gt.Skills.Count(s =>
                        extracted.Skills.Any(e => e.Contains(s, StringComparison.OrdinalIgnoreCase))
                        || extracted.Skills.Any(e => s.Contains(e, StringComparison.OrdinalIgnoreCase)));
                    correct += matched;
                    if (matched == 0)
                        errors.Add(new AccuracyError(item.Id, "Skills",
                            string.Join(",", gt.Skills), string.Join(",", extracted.Skills)));
                    total += gt.Skills.Length;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JD解析测试失败 Id={Id}", item.Id);
                total += 5;
            }
        }

        return new AccuracyMetric(total == 0 ? 0 : (double)correct / total, total, correct, errors);
    }

    // ═══ Resume Extraction Test: 真实调用AI提取后与ground truth对比 ═══

    public async Task<AccuracyMetric> TestResumeExtractionAsync(List<ResumeTestItem> testSet)
    {
        var correct = 0; var total = 0; var errors = new List<AccuracyError>();

        foreach (var item in testSet)
        {
            try
            {
                var prompt = BuildResumeExtractPrompt(item.RawText);
                var aiResponse = await _ai.ChatAsync(prompt);
                var extracted = ParseResumeExtraction(aiResponse);
                var gt = item.GroundTruth;

                // Name match
                if (FuzzyMatch(extracted.Name, gt.Name))
                    correct++;
                else
                    errors.Add(new AccuracyError(item.Id, "Name", gt.Name, extracted.Name));
                total++;

                // Education match
                if (FuzzyContains(gt.Education ?? "", extracted.Education))
                    correct++;
                else
                    errors.Add(new AccuracyError(item.Id, "Education",
                        gt.Education ?? "", extracted.Education));
                total++;

                // WorkYears within ±1
                if (gt.WorkYears.HasValue && extracted.WorkYears.HasValue)
                {
                    if (Math.Abs(gt.WorkYears.Value - extracted.WorkYears.Value) <= 1)
                        correct++;
                    else
                        errors.Add(new AccuracyError(item.Id, "WorkYears",
                            gt.WorkYears.ToString()!, extracted.WorkYears.ToString()!));
                    total++;
                }

                // Skills overlap
                if (gt.Skills.Length > 0 && extracted.Skills.Length > 0)
                {
                    var matched = gt.Skills.Count(s =>
                        extracted.Skills.Any(e => e.Contains(s, StringComparison.OrdinalIgnoreCase)));
                    correct += matched;
                    if (matched == 0)
                        errors.Add(new AccuracyError(item.Id, "Skills",
                            string.Join(",", gt.Skills), string.Join(",", extracted.Skills)));
                    total += gt.Skills.Length;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "简历提取测试失败 Id={Id}", item.Id);
                total += 3;
            }
        }

        return new AccuracyMetric(total == 0 ? 0 : (double)correct / total, total, correct, errors);
    }

    // ═══ Matching Test: 真实调用AI评分后与预期对比 ═══

    public async Task<AccuracyMetric> TestMatchingAsync(List<MatchTestItem> testSet)
    {
        var correct = 0; var total = testSet.Count; var errors = new List<AccuracyError>();

        foreach (var item in testSet)
        {
            try
            {
                var prompt = BuildMatchingPrompt(item.Resume.RawText, item.JD.RawText);
                var aiResponse = await _ai.ChatAsync(prompt);
                var score = ParseMatchScore(aiResponse);
                var predictedMatch = score >= 70;

                if (predictedMatch == item.ShouldMatch)
                    correct++;
                else
                    errors.Add(new AccuracyError(item.Id, "MatchResult",
                        $"expected={item.ShouldMatch}", $"predicted={predictedMatch}(score={score})"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "匹配测试失败 Id={Id}", item.Id);
            }
        }

        return new AccuracyMetric(total == 0 ? 0 : (double)correct / total, total, correct, errors);
    }

    // ═══ Test Set Generators ═══

    public async Task<List<JDTestItem>> GenerateJDTestSetAsync(int count)
    {
        var items = new List<JDTestItem>();
        var rnd = new Random(42);

        for (int i = 0; i < count; i++)
        {
            var job = JobPositions[rnd.Next(JobPositions.Length)];
            var dept = ITDepts[rnd.Next(ITDepts.Length)];
            var loc = Locations[rnd.Next(Locations.Length)];
            var salMin = rnd.Next(10, 40);
            var salMax = salMin + rnd.Next(5, 25);
            var skills = GenerateSkills(rnd);
            var edu = EducationLevels[rnd.Next(EducationLevels.Length)];
            var years = rnd.Next(1, 8);

            var rawText = GenerateRealisticJD(job, dept, loc, salMin, salMax, years, edu, skills);
            var gt = new JDGroundTruth(job, dept, loc, skills, salMin, salMax);

            items.Add(new JDTestItem($"JD-{i + 1:D3}", rawText, gt));
        }

        return items;
    }

    public async Task<List<ResumeTestItem>> GenerateResumeTestSetAsync(int count)
    {
        var items = new List<ResumeTestItem>();
        var rnd = new Random(123);

        for (int i = 0; i < count; i++)
        {
            var name = $"候选人{100 + i}";
            var phone = $"138{10000000 + rnd.Next(89999999)}";
            var edu = EducationLevels[rnd.Next(EducationLevels.Length)];
            var years = rnd.Next(0, 12);
            var skills = GenerateSkills(rnd);
            var rawText = GenerateRealisticResume(name, phone, edu, years, skills);
            var gt = new ResumeGroundTruth(name, phone, $"{name}@example.com", edu, years, skills, null, null);

            items.Add(new ResumeTestItem($"R-{i + 1:D3}", rawText, gt));
        }

        return items;
    }

    public async Task<List<MatchTestItem>> GenerateMatchTestSetAsync(int count)
    {
        var jds = await GenerateJDTestSetAsync(count);
        var resumes = await GenerateResumeTestSetAsync(count);
        var matches = new List<MatchTestItem>();
        var rnd = new Random(999);

        for (int i = 0; i < count; i++)
        {
            var shouldMatch = rnd.NextDouble() > 0.3;
            var jd = shouldMatch ? jds[i] : jds[rnd.Next(jds.Count)];
            var resume = resumes[i];
            var expectedScore = shouldMatch ? rnd.Next(70, 99) : rnd.Next(30, 65);

            matches.Add(new MatchTestItem($"M-{i + 1:D3}", resume, jd, shouldMatch, expectedScore));
        }

        return matches;
    }

    // ═══ AI Prompt Builders ═══

    private static string BuildJDParsePrompt(string rawText)
    {
        return $$"""
从以下招聘JD文本中提取结构化信息，只返回JSON，不要任何其他文字：
{{rawText}}

JSON格式：
{"Title":"岗位名称","Dept":"部门","Location":"工作地点","Skills":["技能1","技能2"]}
""";
    }

    private static string BuildResumeExtractPrompt(string rawText)
    {
        return $$"""
从以下简历文本中提取结构化信息，只返回JSON，不要任何其他文字：
{{rawText}}

JSON格式：
{"Name":"姓名","Education":"学历","WorkYears":数字,"Skills":["技能1","技能2"]}
""";
    }

    private static string BuildMatchingPrompt(string resumeText, string jdText)
    {
        return $$"""
评估以下简历与岗位的匹配度（0-100分），只返回数字分数，不要任何其他文字：

【岗位要求】
{{jdText}}

【候选人简历】
{{resumeText}}

匹配度分数：
""";
    }

    // ═══ AI Response Parsers ═══

    private static (string Title, string Dept, string Location, string[] Skills) ParseJDExtraction(string aiResponse)
    {
        try
        {
            var json = ExtractJson(aiResponse);
            var doc = JsonDocument.Parse(json).RootElement;
            return (
                doc.TryGetProperty("Title", out var t) ? t.GetString() ?? "" : "",
                doc.TryGetProperty("Dept", out var d) ? d.GetString() ?? "" : "",
                doc.TryGetProperty("Location", out var l) ? l.GetString() ?? "" : "",
                doc.TryGetProperty("Skills", out var s) ? s.EnumerateArray().Select(x => x.GetString()!).ToArray() : Array.Empty<string>()
            );
        }
        catch { return ("", "", "", Array.Empty<string>()); }
    }

    private static (string Name, string Education, int? WorkYears, string[] Skills) ParseResumeExtraction(string aiResponse)
    {
        try
        {
            var json = ExtractJson(aiResponse);
            var doc = JsonDocument.Parse(json).RootElement;
            return (
                doc.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "",
                doc.TryGetProperty("Education", out var e) ? e.GetString() ?? "" : "",
                doc.TryGetProperty("WorkYears", out var w) && w.TryGetInt32(out var wy) ? wy : null,
                doc.TryGetProperty("Skills", out var s) ? s.EnumerateArray().Select(x => x.GetString()!).ToArray() : Array.Empty<string>()
            );
        }
        catch { return ("", "", null, Array.Empty<string>()); }
    }

    private static int ParseMatchScore(string aiResponse)
    {
        var digits = new string(aiResponse.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var score))
            return score > 100 ? 100 : score;
        return 50;
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];
        return text;
    }

    // ═══ Comparison Helpers ═══

    private static bool FuzzyMatch(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
            return false;
        return expected.Trim().Contains(actual.Trim(), StringComparison.OrdinalIgnoreCase)
            || actual.Trim().Contains(expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool FuzzyContains(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
            return false;
        return expected.Trim().Contains(actual.Trim(), StringComparison.OrdinalIgnoreCase)
            || actual.Trim().Contains(expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    // ═══ Test Data Generators ═══

    private static string[] GenerateSkills(Random rnd)
    {
        var pool = new[] { "Java", "Spring", "MySQL", "Redis", "Docker", "Kubernetes", "Python",
            "FastAPI", "PostgreSQL", "MongoDB", "React", "Vue", "TypeScript", "Node.js", "Go",
            "C++", "Git", "CI/CD", "Kafka", "Elasticsearch", "AWS", "Azure", "TensorFlow",
            "PyTorch", "Spark", "Flink", "Hadoop", "Linux", "Nginx", "RabbitMQ" };
        var count = rnd.Next(3, 8);
        return pool.OrderBy(_ => rnd.Next()).Take(count).ToArray();
    }

    private static string GenerateRealisticJD(string job, string dept, string loc, int salMin, int salMax,
        int years, string edu, string[] skills)
    {
        return @$"招聘岗位：{job}
部门：{dept}
工作地点：{loc}
薪资：{salMin}K-{salMax}K

岗位职责：
1. 负责公司核心业务系统的设计与开发
2. 参与技术方案评审和系统架构优化
3. 编写高质量代码，保证系统稳定性和可扩展性

任职要求：
1. {edu}及以上学历，{years}年以上{job}开发经验
2. 精通{string.Join("、", skills.Take(3))}
3. 熟悉{string.Join("、", skills.Skip(3).Take(3))}
4. 良好的沟通能力和团队协作精神";
    }

    private static string GenerateRealisticResume(string name, string phone, string edu,
        int years, string[] skills)
    {
        return @$"姓名：{name}
电话：{phone}
邮箱：{name}@example.com
学历：{edu}
工作年限：{years}年

技能：{string.Join("、", skills)}

工作经历：
- 2019-2023 某科技有限公司 {string.Join("、", skills.Take(2))} 开发工程师
  负责核心模块开发，使用{string.Join("、", skills.Take(4))}技术栈

- 2017-2019 某互联网公司 初级开发工程师
  参与多个项目开发，积累了扎实的编程基础";
    }

    private static string GenerateSummary(AccuracyMetric jd, AccuracyMetric resume, AccuracyMetric match)
    {
        var jdPct = jd.Accuracy * 100;
        var resPct = resume.Accuracy * 100;
        var matPct = match.Accuracy * 100;
        var passed = jdPct >= 85 && resPct >= 85 && matPct >= 80;

        return @$"评测结果：JD解析准确率 {jdPct:F1}%，简历提取准确率 {resPct:F1}%，匹配准确率 {matPct:F1}%。
{(passed ? "✅ 三项指标均达标！" : "⚠️ 部分指标未达标，需进一步优化：")}
- JD解析：{jd.CorrectFields}/{jd.TotalFields} 字段正确，{jd.Errors.Count} 个错误
- 简历提取：{resume.CorrectFields}/{resume.TotalFields} 字段正确，{resume.Errors.Count} 个错误
- 人岗匹配：{match.CorrectFields}/{match.TotalFields} 对正确，{match.Errors.Count} 个错误";
    }
}
