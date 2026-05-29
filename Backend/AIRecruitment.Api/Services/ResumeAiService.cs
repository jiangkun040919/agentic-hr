using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Options;

namespace AIRecruitment.Api.Services;

public interface IResumeAiService
{
    Task<ParseResult> ParseResumeAsync(string resumeText);
    Task<MatchScoreResult> ScoreMatchAsync(Candidate c, string jobDescription);
    Task<InterviewGuideResult> GenerateInterviewGuideAsync(Candidate c, string jd, MatchScoreResult? matchResult);
}

// ═══ 简历解析结果 ═══
public class ParseResult
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public EducationInfo? Education { get; set; }
    public int WorkYears { get; set; }
    public List<ResumeSkill> Skills { get; set; } = new();
    public List<WorkExp> WorkExperience { get; set; } = new();
    public List<ResumeProject> Projects { get; set; } = new();
    public List<EduHistory> EducationHistory { get; set; } = new();
    public List<string> Certifications { get; set; } = new();
    public List<ResumeLanguage> Languages { get; set; } = new();
    public string ExtractionQuality { get; set; } = "";
    public string AnalysisMode { get; set; } = "";
    public string AnalyzedAt { get; set; } = "";
}

public class ResumeSkill
{
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
    public int Years { get; set; }
    public string Confidence { get; set; } = "";
}

public class ResumeProject
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public List<string> TechStack { get; set; } = new();
    public string Description { get; set; } = "";
}

public class EduHistory
{
    public string School { get; set; } = "";
    public string Degree { get; set; } = "";
    public string Major { get; set; } = "";
    public int StartYear { get; set; }
    public int EndYear { get; set; }
}

public class ResumeLanguage
{
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
}

[Newtonsoft.Json.JsonConverter(typeof(EducationInfoConverter))]
public class EducationInfo
{
    public string Level { get; set; } = "";
    public string Major { get; set; } = "";
    public string School { get; set; } = "";
}

public class EducationInfoConverter : Newtonsoft.Json.JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(EducationInfo);
    public override object? ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object? existingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
        if (reader.TokenType == Newtonsoft.Json.JsonToken.String)
            return new EducationInfo { Level = reader.Value?.ToString() ?? "" };
        if (reader.TokenType == Newtonsoft.Json.JsonToken.StartObject)
        {
            var obj = new EducationInfo();
            while (reader.Read() && reader.TokenType != Newtonsoft.Json.JsonToken.EndObject)
            {
                if (reader.TokenType == Newtonsoft.Json.JsonToken.PropertyName)
                {
                    var prop = reader.Value?.ToString();
                    reader.Read();
                    switch (prop) { case "level": obj.Level = reader.Value?.ToString() ?? ""; break; case "major": obj.Major = reader.Value?.ToString() ?? ""; break; case "school": obj.School = reader.Value?.ToString() ?? ""; break; }
                }
            }
            return obj;
        }
        return null;
    }
    public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object? value, Newtonsoft.Json.JsonSerializer serializer)
    {
        var edu = value as EducationInfo;
        writer.WriteStartObject();
        writer.WritePropertyName("level"); writer.WriteValue(edu?.Level ?? "");
        writer.WritePropertyName("major"); writer.WriteValue(edu?.Major ?? "");
        writer.WritePropertyName("school"); writer.WriteValue(edu?.School ?? "");
        writer.WriteEndObject();
    }
}

public class WorkExp
{
    public string Company { get; set; } = "";
    public string Title { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string Description { get; set; } = "";
}

// ═══ 匹配评分结果 ═══
public class MatchScoreResult
{
    public int Overall { get; set; }
    public int SkillMatch { get; set; }
    public int ExperienceMatch { get; set; }
    public int EducationMatch { get; set; }
    public int FitScore { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> Gaps { get; set; } = new();
    public string Recommendation { get; set; } = "";
    public string HiringSuggestion { get; set; } = "";
    public string LevelEstimate { get; set; } = "";
    public List<string> InterviewFocus { get; set; } = new();
}

// ═══ 面试建议结果 ═══
public class InterviewGuideResult
{
    public string Strategy { get; set; } = "";
    public List<string> FocusTags { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<IQItem> Questions { get; set; } = new();
    public string SuggestedDuration { get; set; } = "";
    public EvalRubric? Evaluation { get; set; }
}

public class IQItem
{
    public string Type { get; set; } = "";
    public string Category { get; set; } = "";
    public string Question { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string? ExpectedAnswer { get; set; }
}

public class EvalRubric
{
    public int TechnicalWeight { get; set; }
    public int ExperienceWeight { get; set; }
    public int CommunicationWeight { get; set; }
    public int CultureFitWeight { get; set; }
}

// ═══ 服务实现 ═══
public class ResumeAiService : IResumeAiService
{
    private readonly HttpClient _httpClient;
    private readonly AIOptions _aiOptions;
    private readonly ILogger<ResumeAiService> _logger;

    public ResumeAiService(IOptions<AIOptions> aiOptions, ILogger<ResumeAiService> logger)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _aiOptions = aiOptions.Value;
        _logger = logger;
    }

    private async Task<string> CallAIAsync(string systemPrompt, string userPrompt, double temperature)
    {
        if (string.IsNullOrEmpty(_aiOptions.ApiKey))
            throw new Exception("AI API Key未配置");
        var endpoint = $"{_aiOptions.BaseUrl}/text/chatcompletion_v2";
        var body = new { model = _aiOptions.Model, messages = new[] { new { role = "system", content = systemPrompt }, new { role = "user", content = userPrompt } }, temperature };
        var json = JsonConvert.SerializeObject(body);
        var http = new StringContent(json, Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_aiOptions.ApiKey}");
        var resp = await _httpClient.PostAsync(endpoint, http);
        resp.EnsureSuccessStatusCode();
        var raw = await resp.Content.ReadAsStringAsync();
        var parsed = JsonConvert.DeserializeObject<dynamic>(raw);
        string content = (parsed?.choices?[0]?.message?.content?.ToString() ?? "").Trim();
        if (content.StartsWith("```json")) content = content[7..];
        else if (content.StartsWith("```")) content = content[3..];
        if (content.EndsWith("```")) content = content[..^3];
        return content.Trim();
    }

    public async Task<ParseResult> ParseResumeAsync(string resumeText)
    {
        try
        {
            var system = @"你是专业简历解析引擎。从简历原文提取所有结构化信息，绝不编造。
技能标注掌握程度(精通/熟练/了解)、使用年限、置信度(confirmed/inferred)。
教育经历按时间线列出。证书和语言能力单独列出。没有的信息用空值。
返回JSON：{""name"":"""",""phone"":"""",""email"":"""",""education"":{""level"":"""",""major"":"""",""school"":""""},""workYears"":0,""skills"":[{""name"":"""",""level"":"""",""years"":0,""confidence"":""""}],""workExperience"":[{""company"":"""",""title"":"""",""startDate"":"""",""endDate"":"""",""description"":""""}],""projects"":[{""name"":"""",""role"":"""",""techStack"":[],""description"":""""}],""educationHistory"":[{""school"":"""",""degree"":"""",""major"":"""",""startYear"":0,""endYear"":0}],""certifications"":[],""languages"":[{""name"":"""",""level"":""""}]}";
            var user = $"解析以下简历：\n\n{resumeText[..Math.Min(resumeText.Length, 5000)]}";
            var raw = await CallAIAsync(system, user, 0.1);
            var parsed = JsonConvert.DeserializeObject<ParseResult>(raw);
            if (parsed != null) { parsed.AnalysisMode = "AI深度解析"; parsed.AnalyzedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"); }
            return parsed ?? new ParseResult();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "简历解析失败"); return new ParseResult(); }
    }

    public async Task<MatchScoreResult> ScoreMatchAsync(Candidate c, string jobDescription)
    {
        try
        {
            var system = @"你是资深招聘顾问。根据候选人背景和岗位要求，给出五维匹配评分和录用建议。
必须返回JSON：{""overall"":0-100,""skillMatch"":0-100,""experienceMatch"":0-100,""educationMatch"":0-100,""fitScore"":0-100,""strengths"":[""优势""],""gaps"":[""差距""],""recommendation"":""100字以内综合建议"",""hiringSuggestion"":""建议录用/建议面试/建议复试/暂缓"",""levelEstimate"":""初级/中级/高级/资深"",""interviewFocus"":[""重点1"",""重点2"",""重点3""]}";
            var user = $"候选人：学历{c.Education ?? "未知"}，{c.WorkYears ?? 0}年经验\n岗位：{jobDescription[..Math.Min(jobDescription.Length, 1000)]}";
            var raw = await CallAIAsync(system, user, 0.3);
            return JsonConvert.DeserializeObject<MatchScoreResult>(raw) ?? new MatchScoreResult();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "匹配评分失败"); return new MatchScoreResult(); }
    }

    public async Task<InterviewGuideResult> GenerateInterviewGuideAsync(Candidate c, string jd, MatchScoreResult? matchResult)
    {
        try
        {
            var matchText = matchResult != null ? $"匹配分{ matchResult.Overall}，录用建议{ matchResult.HiringSuggestion }" : "无";
            var system = @"你是资深HR面试官。为候选人定制面试方案。包含面试策略、风险提示、10-15道分类面试题（每道标注类别、目的、参考答案要点）、建议时长、评分维度权重。
返回JSON：{""strategy"":""面试策略"",""focusTags"":[""标签""],""warnings"":[""风险提示""],""questions"":[{""type"":""tech/experience/star/scenario"",""category"":""技术能力/项目经验/行为面试/场景模拟"",""question"":"""",""purpose"":"""",""expectedAnswer"":""参考答案要点""}],""suggestedDuration"":""30/45/60分钟"",""evaluation"":{""technicalWeight"":40,""experienceWeight"":30,""communicationWeight"":15,""cultureFitWeight"":15}}";
            var user = $"候选人：学历{c.Education ?? "未知"}，{c.WorkYears ?? 0}年经验\n岗位：{jd[..Math.Min(jd.Length, 1000)]}\n匹配评估：{matchText}";
            var raw = await CallAIAsync(system, user, 0.5);
            return JsonConvert.DeserializeObject<InterviewGuideResult>(raw) ?? new InterviewGuideResult();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "面试建议生成失败"); return new InterviewGuideResult(); }
    }
}
