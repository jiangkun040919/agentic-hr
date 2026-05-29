using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

/// <summary>
/// 招聘策略决策 API — AI 驱动的战略分析
/// </summary>
[ApiController]
[Route("api/strategy")]
[Authorize(Roles = "hr,admin")]
public class StrategyController : ControllerBase
{
    private readonly AppDbContext _ctx;
    private readonly IAIService _ai;

    public StrategyController(AppDbContext ctx, IAIService ai)
    {
        _ctx = ctx;
        _ai = ai;
    }

    /// <summary>技能缺口分析 — 公司需求 vs 市场热门技能</summary>
    [HttpGet("skill-gap")]
    public async Task<IActionResult> GetSkillGap()
    {
        // 公司内部需求的技能（从岗位 Requirements 提取）
        var internalSkills = await _ctx.Jobs
            .Where(j => j.Status == 1 && j.Requirements != null)
            .Select(j => j.Requirements)
            .ToListAsync();

        var skillDemand = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var skillNames = new[] {
            "Java", "Python", "Go", "Rust", "TypeScript", "JavaScript",
            "Vue", "React", "Angular", "Spring", "Docker", "Kubernetes",
            "AI", "机器学习", "深度学习", "大数据", "SQL", "MySQL",
            "Redis", "Kafka", "微服务", "DevOps", "Linux", "AWS",
            "Node.js", "Flutter", "小程序", ".NET", "C++", "C#"
        };

        foreach (var req in internalSkills)
        {
            foreach (var skill in skillNames)
            {
                if (req != null && req.Contains(skill, StringComparison.OrdinalIgnoreCase))
                {
                    skillDemand[skill] = skillDemand.GetValueOrDefault(skill) + 1;
                }
            }
        }

        // 模拟市场供给指数（基于常见行业数据，可替换为真实爬取数据）
        var marketSupply = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Java"] = 0.85, ["Python"] = 0.80, ["Go"] = 0.55, ["Rust"] = 0.30,
            ["TypeScript"] = 0.75, ["JavaScript"] = 0.90, ["Vue"] = 0.70, ["React"] = 0.75,
            ["Spring"] = 0.60, ["Docker"] = 0.80, ["Kubernetes"] = 0.65,
            ["AI"] = 0.35, ["机器学习"] = 0.30, ["大数据"] = 0.45, ["SQL"] = 0.90,
            ["Redis"] = 0.75, ["Kafka"] = 0.50, ["微服务"] = 0.55, ["DevOps"] = 0.60,
            ["Linux"] = 0.85, ["Node.js"] = 0.70, ["Flutter"] = 0.40, [".NET"] = 0.50,
            ["C++"] = 0.55, ["C#"] = 0.50, ["AWS"] = 0.60,
        };

        var gapData = skillDemand
            .Where(kv => kv.Value >= 2)
            .Select(kv => new
            {
                skill = kv.Key,
                demand = kv.Value,
                supply = marketSupply.GetValueOrDefault(kv.Key, 0.5),
                gap = Math.Round(kv.Value / 10.0 - marketSupply.GetValueOrDefault(kv.Key, 0.5), 2),
            })
            .OrderByDescending(x => x.gap)
            .Take(8)
            .ToList();

        return Ok(new { code = 200, data = gapData });
    }

    /// <summary>AI 战略摘要 — 聚合所有统计数据，LLM 生成洞察</summary>
    [HttpGet("ai-summary")]
    public async Task<IActionResult> GetAiSummary()
    {
        try
        {
            // 收集关键数据
            var totalJobs = await _ctx.Jobs.CountAsync(j => j.Status == 1);
            var totalDeliveries = await _ctx.Deliveries.CountAsync();
            var hired = await _ctx.Deliveries.CountAsync(d => d.Status >= 4);
            var interviewing = await _ctx.Deliveries.CountAsync(d => d.Status == 2);
            var pending = await _ctx.Deliveries.CountAsync(d => d.Status == 0);

            // 技能 Top 5
            var topSkills = await _ctx.Jobs
                .Where(j => j.Status == 1 && j.Requirements != null)
                .Select(j => j.Requirements)
                .ToListAsync();

            var skillCounts = new Dictionary<string, int>();
            foreach (var req in topSkills)
            {
                if (req == null) continue;
                var skills = new[] { "Java", "Python", "Go", "Vue", "React", "AI", "Docker", "Kubernetes", "TypeScript" };
                foreach (var s in skills)
                    if (req.Contains(s, StringComparison.OrdinalIgnoreCase))
                        skillCounts[s] = skillCounts.GetValueOrDefault(s) + 1;
            }
            var top5 = skillCounts.OrderByDescending(kv => kv.Value).Take(5)
                .Select(kv => $"{kv.Key}({kv.Value}个岗位)");

            var conversionRate = totalDeliveries > 0 ? Math.Round((double)hired / totalDeliveries * 100, 1) : 0;

            var prompt = $@"你是招聘策略分析师。根据以下数据，给出3条简短的招聘战略建议（每条不超过50字）：

- 活跃岗位: {totalJobs}个
- 总投递: {totalDeliveries}份
- 已入职: {hired}人 (转化率{conversionRate}%)
- 面试中: {interviewing}人
- 待处理: {pending}人
- Top5 技能需求: {string.Join(", ", top5)}

格式：每条一行，以""⚠️""或""🔥""或""📊""开头。";

            var result = await _ai.ChatAsync(prompt);

            // 清理 MiniMax 可能返回的 think 标签
            var cleaned = System.Text.RegularExpressions.Regex.Replace(
                result ?? "", @"<think>.*?</think>", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            cleaned = cleaned.Trim();

            var lines = cleaned.Split('\n')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Take(3)
                .ToList();

            return Ok(new
            {
                code = 200,
                data = new
                {
                    summary = lines,
                    stats = new { totalJobs, totalDeliveries, hired, interviewing, pending, conversionRate },
                }
            });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 200, data = new { summary = new[] { "📊 系统正常运行", "📋 暂无特殊建议" }, stats = new { } } });
        }
    }
}
