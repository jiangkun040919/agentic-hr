using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIRecruitment.Api.Controllers;

/// <summary>
/// AI 智能匹配推荐 — 对话式求职核心
/// </summary>
[ApiController]
[Route("api/match")]
public class MatchController : ControllerBase
{
    private readonly AppDbContext _ctx;
    private readonly IAIService _ai;

    public MatchController(AppDbContext ctx, IAIService ai)
    {
        _ctx = ctx;
        _ai = ai;
    }

    /// <summary>
    /// AI 推荐：输入求职意向或简历文本，返回匹配岗位
    /// </summary>
    [HttpPost("ai-recommend")]
    public async Task<IActionResult> AiRecommend([FromBody] RecommendRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Input))
            return Ok(new { code = 400, message = "请输入求职意向或上传简历" });

        try
        {
            // Step 1: AI 解析输入，提取技能和偏好
            var parsed = await ParseIntentAsync(req.Input);
            var skills = parsed.skills;
            var preferCity = parsed.city;
            var preferSalary = parsed.salary;

            // Step 2: 从岗位中匹配
            var allJobs = await _ctx.Jobs
                .Where(j => j.Status == 1)
                .Select(j => new
                {
                    j.JobId, j.Title, j.Dept, j.Location, j.SalaryMin, j.SalaryMax,
                    j.Requirements, j.JD, j.HeadCount, j.CreatedAt,
                })
                .ToListAsync();

            var scored = new List<object>();
            foreach (var job in allJobs)
            {
                int score = 0;
                var matched = new List<string>();
                var missing = new List<string>();

                foreach (var skill in skills)
                {
                    if (!string.IsNullOrEmpty(job.Requirements) &&
                        job.Requirements.Contains(skill, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 20;
                        matched.Add(skill);
                    }
                    else if (!string.IsNullOrEmpty(job.Title) &&
                             job.Title.Contains(skill, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 15;
                        matched.Add(skill);
                    }
                    else
                    {
                        missing.Add(skill);
                    }
                }

                // 城市匹配加分
                if (!string.IsNullOrEmpty(preferCity) &&
                    !string.IsNullOrEmpty(job.Location) &&
                    job.Location.Contains(preferCity))
                {
                    score += 10;
                }

                // 标题关键词匹配
                if (skills.Any(s => job.Title?.Contains(s, StringComparison.OrdinalIgnoreCase) == true))
                {
                    score += 10;
                }

                int matchRate = Math.Min(98, Math.Max(30, score));
                if (matchRate < 40) continue; // 过滤低匹配

                scored.Add(new
                {
                    job.JobId, job.Title, job.Dept, job.Location,
                    job.SalaryMin, job.SalaryMax, job.HeadCount,
                    job.CreatedAt,
                    matchRate,
                    matched,
                    missing,
                    jd = (job.JD ?? "")[..Math.Min(200, job.JD?.Length ?? 0)],
                });
            }

            var top = scored
                .Cast<dynamic>()
                .OrderByDescending(j => (int)j.matchRate)
                .Take(6)
                .ToList();

            // Step 3: AI 生成个性化建议
            var topInfo = string.Join("; ", top.Select(j =>
                $"{(string)j.Title}({(string)j.Location},{(int)j.SalaryMin}-{(int)j.SalaryMax}K,匹配{(int)j.matchRate}%)"));

            var advicePrompt = $@"你是一个友好的招聘顾问。
求职者技能：{string.Join("、", skills)}
期望城市：{preferCity ?? "不限"}
匹配岗位：{topInfo}

请给出一条简短的个性化求职建议（不超过50字），语气要温暖、有洞察力。直接返回建议文本。";
            var advice = await _ai.ChatAsync(advicePrompt);
            advice = Regex.Replace(advice ?? "", @"<think>.*?</think>", "", RegexOptions.Singleline).Trim();
            if (advice.Length > 100) advice = advice[..100];

            return Ok(new
            {
                code = 200,
                data = new
                {
                    parsed = new { skills, city = preferCity, salary = preferSalary },
                    recommendations = top,
                    advice = advice ?? "根据你的技能，为你找到以下匹配岗位",
                }
            });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = $"AI分析失败: {ex.Message}" });
        }
    }

    private async Task<(List<string> skills, string? city, string? salary)> ParseIntentAsync(string input)
    {
        // 用 AI 解析求职意向
        var prompt = $@"从以下求职者输入中提取信息，返回JSON：
{{""skills"":[""技能1"",""技能2""],""city"":""期望城市或null"",""salary"":""期望薪资范围或null""}}

输入：{input[..Math.Min(input.Length, 1000)]}

直接返回JSON，不要解释。";

        var result = await _ai.ChatAsync(prompt);
        result = Regex.Replace(result ?? "", @"<think>.*?</think>", "", RegexOptions.Singleline).Trim();

        try
        {
            var json = result;
            if (json.StartsWith("```")) json = json[(json.IndexOf('\n') + 1)..];
            if (json.EndsWith("```")) json = json[..^3];
            json = json.Trim();

            var doc = JsonDocument.Parse(json);
            var skills = doc.RootElement.TryGetProperty("skills", out var sk) &&
                         sk.ValueKind == JsonValueKind.Array
                ? sk.EnumerateArray().Select(s => s.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
                : new List<string>();

            var city = doc.RootElement.TryGetProperty("city", out var ct) && ct.ValueKind == JsonValueKind.String
                ? ct.GetString() : null;

            var salary = doc.RootElement.TryGetProperty("salary", out var sa) && sa.ValueKind == JsonValueKind.String
                ? sa.GetString() : null;

            if (skills.Count == 0) skills = ExtractSkillsSimple(input);
            return (skills, city, salary);
        }
        catch
        {
            return (ExtractSkillsSimple(input), null, null);
        }
    }

    private List<string> ExtractSkillsSimple(string input)
    {
        var skillBank = new[] {
            "Java","Python","Go","Rust","C++","C#","JavaScript","TypeScript",
            "Vue","React","Angular","Node.js","Spring","Django","Flask","FastAPI",
            "MySQL","PostgreSQL","MongoDB","Redis","Docker","Kubernetes","K8s",
            "AI","机器学习","深度学习","NLP","CV","大模型","LLM","PyTorch","TensorFlow",
            "前端","后端","全栈","微服务","DevOps","Linux","AWS","Azure","阿里云",
            "产品","测试","运维","安全","数据分析","大数据","Hadoop","Spark","Flink",
        };
        return skillBank.Where(s => input.Contains(s, StringComparison.OrdinalIgnoreCase)).Take(8).ToList();
    }
}

public class RecommendRequest
{
    public string? Input { get; set; }
}
