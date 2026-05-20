using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

public class ParseResult
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public EducationInfo? Education { get; set; }
    public int WorkYears { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<WorkExp> WorkExperience { get; set; } = new();
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
                    switch (prop)
                    {
                        case "level": obj.Level = reader.Value?.ToString() ?? ""; break;
                        case "major": obj.Major = reader.Value?.ToString() ?? ""; break;
                        case "school": obj.School = reader.Value?.ToString() ?? ""; break;
                    }
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
}

public class InterviewGuideResult
{
    public string Strategy { get; set; } = "";
    public List<string> FocusTags { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<InterviewQuestion> Questions { get; set; } = new();
}

public class InterviewQuestion
{
    public string Type { get; set; } = "";
    public string Question { get; set; } = "";
    public string Purpose { get; set; } = "";
}

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
            throw new Exception("AI API Key未配置，请在环境变量中设置 AI__ApiKey");

        var endpoint = $"{_aiOptions.BaseUrl}/text/chatcompletion_v2";
        var requestBody = new
        {
            model = _aiOptions.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature
        };

        var jsonContent = JsonConvert.SerializeObject(requestBody);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_aiOptions.ApiKey}");

        var response = await _httpClient.PostAsync(endpoint, httpContent);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"AI服务调用失败: {response.StatusCode} - {err}");
        }
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<dynamic>(body);
        string content = (result?.choices?[0]?.message?.content?.ToString() ?? "").Trim();
        if (content.StartsWith("```json")) content = content[7..];
        else if (content.StartsWith("```")) content = content[3..];
        if (content.EndsWith("```")) content = content[..^3];
        return content.Trim();
    }

    public async Task<ParseResult> ParseResumeAsync(string resumeText)
    {
        try
        {
            var system = @"你是专业简历解析引擎，从简历文本中提取结构化字段。

重要：返回的JSON格式如下，education必须是对象（不是字符串）：
{
  ""name"": ""姓名"",
  ""phone"": ""电话"",
  ""email"": ""邮箱"",
  ""education"": { ""level"": ""学历"", ""major"": ""专业"", ""school"": ""学校"" },
  ""workYears"": 0,
  ""skills"": [""技能1"", ""技能2""],
  ""workExperience"": [{ ""company"": ""公司"", ""title"": ""职位"", ""startDate"": ""开始"", ""endDate"": ""结束"", ""description"": ""描述"" }]
}
只返回JSON不输出其他内容";
            var user = $"请解析以下简历文本并返回JSON：\n\n{resumeText}";
            var raw = await CallAIAsync(system, user, 0.1);
            _logger.LogInformation("ParseResume raw: {Raw}", raw[..Math.Min(200, raw.Length)]);
            var parsed = JsonConvert.DeserializeObject<ParseResult>(raw);
            return parsed ?? new ParseResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI简历解析失败，返回空结构");
            return new ParseResult();
        }
    }

    public async Task<MatchScoreResult> ScoreMatchAsync(Candidate c, string jobDescription)
    {
        try
        {
            var system = "你是专业招聘顾问，评估候选人与岗位匹配度，只返回JSON";
            var user = $"""
候选人信息：
- 学历：{c.Education ?? "未知"}
- 工作年限：{c.WorkYears ?? 0}年

岗位描述：
{jobDescription}

请评估匹配度，返回JSON。
""";
            var raw = await CallAIAsync(system, user, 0.3);
            var parsed = JsonConvert.DeserializeObject<MatchScoreResult>(raw);
            return parsed ?? new MatchScoreResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI匹配评分失败，返回空结构");
            return new MatchScoreResult();
        }
    }

    public async Task<InterviewGuideResult> GenerateInterviewGuideAsync(Candidate c, string jd, MatchScoreResult? matchResult)
    {
        try
        {
            var system = "你是经验丰富的HR面试官，根据候选人背景生成针对性面试方案，只返回JSON";
            var matchJson = matchResult != null ? JsonConvert.SerializeObject(matchResult) : "无";
            var user = $"""
候选人：学历{c.Education ?? "未知"}，{c.WorkYears ?? 0}年经验
岗位JD：{jd}
匹配评估：{matchJson}

请生成面试方案JSON。
""";
            var raw = await CallAIAsync(system, user, 0.5);
            var parsed = JsonConvert.DeserializeObject<InterviewGuideResult>(raw);
            return parsed ?? new InterviewGuideResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI面试方案生成失败，返回空结构");
            return new InterviewGuideResult();
        }
    }
}
