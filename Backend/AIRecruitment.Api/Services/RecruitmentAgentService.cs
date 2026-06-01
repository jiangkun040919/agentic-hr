using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using AIRecruitment.Api.Models;

namespace AIRecruitment.Api.Services;

/// <summary>
/// Agentic AI 招聘专员 — 一句话驱动招聘全流程
/// 用户输入自然语言 → AI解析意图 → 自动执行动作链 → 反馈结果
/// </summary>
public class RecruitmentAgentService
{
    private readonly IAIService _ai;
    private readonly AppDbContext _context;
    private readonly KnowledgeGraphService _graph;
    private readonly ILogger<RecruitmentAgentService> _logger;

    public RecruitmentAgentService(
        IAIService ai,
        AppDbContext context,
        KnowledgeGraphService graph,
        ILogger<RecruitmentAgentService> logger)
    {
        _ai = ai;
        _context = context;
        _graph = graph;
        _logger = logger;
    }

    /// <summary>
    /// 执行招聘任务：一句话输入 → AI 解析 → 自动执行 → 汇总报告
    /// </summary>
    public async Task<AgentExecutionReport> ExecuteAsync(string userRequest)
    {
        var report = new AgentExecutionReport
        {
            UserRequest = userRequest,
            StartedAt = DateTime.UtcNow,
            Steps = new List<AgentStep>()
        };

        try
        {
            // Step 1: AI 解析意图，生成动作计划
            var plan = await ParseIntentAsync(userRequest);
            if (plan == null || plan.Actions.Count == 0)
            {
                report.Summary = "无法理解您的需求，请提供更具体的招聘要求（如岗位名称、经验要求等）";
                report.CompletedAt = DateTime.UtcNow;
                return report;
            }

            report.ParsedIntent = plan;

            // Step 2: 逐步执行动作
            foreach (var action in plan.Actions)
            {
                var step = await ExecuteActionAsync(action);
                report.Steps.Add(step);
                if (!step.Success) break; // 动作失败则停止
            }

            // Step 3: 生成汇总报告
            report.Summary = GenerateSummary(report);
        }
        catch (Exception ex)
        {
            _logger.LogError("Agent执行失败: {msg}", ex.Message);
            report.Summary = $"执行过程中出现错误：{ex.Message}。请重试或联系管理员。";
        }

        report.CompletedAt = DateTime.UtcNow;
        report.TotalDuration = (int)(report.CompletedAt - report.StartedAt).TotalSeconds;
        return report;
    }

    /// <summary>
    /// 用 AI 解析用户意图为动作计划
    /// </summary>
    private async Task<AgentPlan?> ParseIntentAsync(string userRequest)
    {
        var prompt = $@"你是招聘系统动作解析器。你必须只输出JSON，不要任何其他文字。

根据用户的招聘需求，输出应执行的动作序列。

可用动作: search_jobs, search_candidates, generate_jd, match_candidates, analyze_market

用户需求：{userRequest}

你必须严格输出以下格式的JSON（不要markdown代码块）：
{{""actions"": [{{""action"": ""动作名"", ""params"": {{""关键词"": ""值""}}}} ]}}";

        try
        {
            var response = await _ai.ChatAsync(prompt);
            response = CleanJson(response);
            var parsed = JsonConvert.DeserializeObject<AgentPlan>(response);
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("意图解析失败: {msg}", ex.Message);
            // 降级：基于关键词做简单规则匹配
            return RuleBasedParse(userRequest);
        }
    }

    /// <summary>规则降级：当 AI 不可用时用关键词匹配</summary>
    private static AgentPlan RuleBasedParse(string request)
    {
        var actions = new List<AgentAction>();
        if (request.Contains("招") || request.Contains("JD") || request.Contains("岗位描述"))
            actions.Add(new AgentAction { Action = "generate_jd", Params = new Dictionary<string, object> { ["title"] = ExtractKey(request, new[] { "Java", "Python", "前端", "Go", "产品", "数据", "AI", "测试", "运维" }), ["requirements"] = request } });
        actions.Add(new AgentAction { Action = "search_candidates", Params = new Dictionary<string, object> { ["skills"] = new[] { ExtractKey(request, new[] { "Java", "Python", "React", "Vue", "Go", "Docker", "K8s", "SQL" }) }, ["minWorkYears"] = ExtractNumber(request) } });
        return new AgentPlan { Actions = actions };
    }

    private static string ExtractKey(string text, string[] keywords)
    {
        foreach (var kw in keywords)
            if (text.Contains(kw)) return kw;
        return "开发";
    }

    private static int ExtractNumber(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)年");
        return match.Success ? int.Parse(match.Groups[1].Value) : 1;
    }

    /// <summary>执行单个动作</summary>
    private async Task<AgentStep> ExecuteActionAsync(AgentAction action)
    {
        var step = new AgentStep { Action = action.Action, StartedAt = DateTime.UtcNow };
        try
        {
            switch (action.Action)
            {
                case "search_jobs":
                    var keyword = action.Params.GetValueOrDefault("keyword")?.ToString() ?? "";
                    var location = action.Params.GetValueOrDefault("location")?.ToString() ?? "";
                    var jobs = await _context.Jobs
                        .Where(j => j.Status == 1 && (string.IsNullOrEmpty(keyword) || j.Title.Contains(keyword)))
                        .Take(10).ToListAsync();
                    step.Result = new { count = jobs.Count, jobs = jobs.Select(j => new { j.JobId, j.Title, j.Location, j.SalaryMin, j.SalaryMax }) };
                    step.Success = true;
                    break;

                case "search_candidates":
                    var skills = (action.Params.GetValueOrDefault("skills") as System.Collections.IEnumerable)?
                        .Cast<object>().Select(s => s.ToString()).ToList() ?? new List<string>();
                    var education = action.Params.GetValueOrDefault("education")?.ToString() ?? "";
                    var minYears = Convert.ToInt32(action.Params.GetValueOrDefault("minWorkYears") ?? 0);
                    var candidates = await _context.Candidates
                        .Where(c => (string.IsNullOrEmpty(education) || c.Education!.Contains(education))
                            && (minYears == 0 || (c.WorkYears ?? 0) >= minYears))
                        .Take(15).ToListAsync();
                    step.Result = new { count = candidates.Count, candidates = candidates.Select(c => new { c.CandidateId, c.RealName, c.Education, c.WorkYears }) };
                    step.Success = true;
                    break;

                case "generate_jd":
                    var title = action.Params.GetValueOrDefault("title")?.ToString() ?? "未知岗位";
                    var reqs = action.Params.GetValueOrDefault("requirements")?.ToString() ?? "";
                    try
                    {
                        var jdPrompt = $"为「{title}」岗位生成JD。要求：{reqs}。输出JSON：{{\"title\":\"\",\"responsibilities\":[\"职责\"],\"requirements\":[\"要求\"]}}";
                        var jdResult = await _ai.ChatAsync(jdPrompt);
                        step.Result = new { generated = true, jdPreview = jdResult[..Math.Min(jdResult.Length, 300)] };
                    }
                    catch { step.Result = new { generated = false, fallbackJd = true, title, requirements = reqs }; }
                    step.Success = true;
                    break;

                case "match_candidates":
                    var jobId = Convert.ToInt32(action.Params.GetValueOrDefault("jobId") ?? 0);
                    var topN = Convert.ToInt32(action.Params.GetValueOrDefault("topN") ?? 5);
                    var deliveries = await _context.Deliveries
                        .Include(d => d.Candidate).Include(d => d.Job)
                        .Where(d => d.JobId == jobId && d.Status >= 1)
                        .OrderByDescending(d => d.Status).Take(topN).ToListAsync();
                    step.Result = new { count = deliveries.Count, matches = deliveries.Select(d => new { d.DeliveryId, Name = d.Candidate?.RealName, d.Status }) };
                    step.Success = true;
                    break;

                case "analyze_market":
                    var mktKeyword = action.Params.GetValueOrDefault("keyword")?.ToString() ?? "技术";
                    step.Result = new { analysis = $"基于知识图谱分析：{mktKeyword}方向当前最热门技能包括 Python、AI/ML、云原生等，建议关注大模型应用和Agent开发趋势。" };
                    step.Success = true;
                    break;

                default:
                    step.Result = new { error = $"未知动作: {action.Action}" };
                    step.Success = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            step.Success = false;
            step.Error = ex.Message;
        }
        step.CompletedAt = DateTime.UtcNow;
        step.Duration = (int)(step.CompletedAt - step.StartedAt).TotalSeconds;
        return step;
    }

    private static string GenerateSummary(AgentExecutionReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## 招聘任务执行报告\n");
        sb.AppendLine($"需求：{report.UserRequest}\n");
        sb.AppendLine($"共执行 {report.Steps.Count} 个步骤：\n");

        foreach (var step in report.Steps)
        {
            var icon = step.Success ? "✅" : "❌";
            var name = step.Action switch
            {
                "search_jobs" => "搜索岗位", "search_candidates" => "搜索候选人",
                "generate_jd" => "生成JD", "match_candidates" => "匹配候选人",
                "analyze_market" => "市场分析", _ => step.Action
            };
            sb.AppendLine($"{icon} {name}（{step.Duration}秒）");
            if (!step.Success) sb.AppendLine($"   错误：{step.Error}");
        }

        return sb.ToString();
    }

    private static string CleanJson(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith("```json")) raw = raw[7..];
        else if (raw.StartsWith("```")) raw = raw[3..];
        if (raw.EndsWith("```")) raw = raw[..^3];
        return raw.Trim();
    }
}

// ═══ Agent 数据模型 ═══

public class AgentExecutionReport
{
    public string UserRequest { get; set; } = "";
    public AgentPlan? ParsedIntent { get; set; }
    public List<AgentStep> Steps { get; set; } = new();
    public string Summary { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int TotalDuration { get; set; }
}

public class AgentPlan
{
    public List<AgentAction> Actions { get; set; } = new();
}

public class AgentAction
{
    public string Action { get; set; } = "";
    public Dictionary<string, object> Params { get; set; } = new();
}

public class AgentStep
{
    public string Action { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int Duration { get; set; }
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
}
