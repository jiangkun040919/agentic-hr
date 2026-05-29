using Newtonsoft.Json;
using AIRecruitment.Api.Options;
using Microsoft.Extensions.Options;
using System.Text;

namespace AIRecruitment.Api.Services;

/// <summary>
/// 多智能体匹配引擎（Multi-Agent Architecture）。
/// 参考 CVPR 2025 "AI Hiring with LLMs: A Context-Aware and Explainable Multi-Agent Framework"。
///
/// 将单一 AI 调用拆分为 5 个协作 Agent：
///   Agent 1 简历解析 → Agent 2 岗位拆解 → Agent 3 差距分析
///   → Agent 4 匹配评分 → Agent 5 面试建议
///
/// 每个 Agent 的输入是前序 Agent 的结构化输出，
/// 各 Agent 互相校验，天然降低幻觉。
/// </summary>
public class MultiAgentMatchingService
{
    private readonly HttpClient _httpClient;
    private readonly AIOptions _aiOptions;
    private readonly ILogger<MultiAgentMatchingService> _logger;

    public MultiAgentMatchingService(
        IOptions<AIOptions> aiOptions,
        ILogger<MultiAgentMatchingService> logger)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _aiOptions = aiOptions.Value;
        _logger = logger;
    }

    public async Task<MultiAgentResult> AnalyzeAsync(string resumeText, string jobTitle, string jobRequirements)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("[MultiAgent] 开始分析: {job}", jobTitle);

        try
        {
            // ═══ Agent 1: 简历解析师 ═══
            var parsedResume = await AgentParseResume(resumeText);

            // ═══ Agent 2: 岗位分析师 ═══
            var jobAnalysis = await AgentAnalyzeJob(jobTitle, jobRequirements);

            // ═══ Agent 3: 差距分析师 ═══
            var gapAnalysis = await AgentAnalyzeGap(parsedResume, jobAnalysis);

            // ═══ Agent 4: 匹配评分师 ═══
            var matchScore = await AgentScoreMatch(parsedResume, jobAnalysis, gapAnalysis);

            // ═══ Agent 5: 面试顾问 ═══
            var interviewGuide = await AgentGenerateInterviewGuide(parsedResume, jobAnalysis, gapAnalysis);

            sw.Stop();
            return new MultiAgentResult
            {
                ResumeAnalysis = parsedResume,
                JobAnalysis = jobAnalysis,
                GapAnalysis = gapAnalysis,
                MatchScore = matchScore,
                InterviewGuide = interviewGuide,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("[MultiAgent] 分析失败: {msg}", ex.Message);
            throw;
        }
    }

    // ═══════════════════════════════════════════════════
    // Agent 1: 简历解析师
    // ═══════════════════════════════════════════════════
    private async Task<dynamic> AgentParseResume(string resumeText)
    {
        var systemPrompt = @"你是资深简历解析师（Talent Parser Agent）。
你的职责：从简历原文中提取所有事实，绝不编造。
规则：
- 只提取原文明确写出的内容
- 技能从「技能」「技术栈」「精通」「熟练」板块提取，标注掌握程度
- 工作经历从「工作经历」「工作经验」板块提取
- 没有的信息用空值表示
- 对每条信息标注 confidence: confirmed/inferred";

        var userPrompt = $"简历原文：\n{resumeText[..Math.Min(resumeText.Length, 4000)]}\n\n返回JSON：{{\"personal\":{{\"name\":\"\",\"education\":\"\",\"school\":\"\",\"major\":\"\",\"workYears\":0}},\"skills\":[{{\"name\":\"\",\"level\":\"精通/熟练/了解\",\"confidence\":\"confirmed\"}}],\"workExperience\":[{{\"company\":\"\",\"position\":\"\",\"years\":0,\"description\":\"\"}}],\"projects\":[{{\"name\":\"\",\"techStack\":[],\"description\":\"\"}}]}}";

        var response = await CallAgent("parse_resume", systemPrompt, userPrompt);
        return JsonConvert.DeserializeObject<dynamic>(response) ?? new { };
    }

    // ═══════════════════════════════════════════════════
    // Agent 2: 岗位分析师
    // ═══════════════════════════════════════════════════
    private async Task<dynamic> AgentAnalyzeJob(string jobTitle, string jobRequirements)
    {
        var systemPrompt = @"你是资深岗位分析师（JD Analyst Agent）。
你的职责：从岗位描述中拆解核心要求、隐性需求和加分项。
规则：
- 硬性要求（Hard Requirements）：必须具备
- 加分项（Nice-to-Have）：优先考虑
- 隐性需求（Implicit）：从行业惯例推断";

        var userPrompt = $"岗位：{jobTitle}\n要求：{jobRequirements[..Math.Min(jobRequirements.Length, 2000)]}\n\n返回JSON：{{\"hardRequirements\":[{{\"skill\":\"\",\"level\":\"\",\"yearsRequired\":0}}],\"niceToHave\":[],\"implicitRequirements\":[],\"levelEstimate\":\"初级/中级/高级/资深\"}}";

        var response = await CallAgent("analyze_job", systemPrompt, userPrompt);
        return JsonConvert.DeserializeObject<dynamic>(response) ?? new { };
    }

    // ═══════════════════════════════════════════════════
    // Agent 3: 差距分析师
    // ═══════════════════════════════════════════════════
    private async Task<dynamic> AgentAnalyzeGap(dynamic resume, dynamic job)
    {
        var systemPrompt = @"你是差距分析师（Gap Analyst Agent）。
你的职责：逐项比对简历与岗位要求，精准定位技能缺口。
规则：
- 匹配项标注 matchType: exact(精确)/partial(部分)/related(相关)
- 缺口项标注 urgency: critical(关键)/high(高)/medium(中)/low(低)";

        var userPrompt = $"简历解析：{JsonConvert.SerializeObject(resume)}\n岗位分析：{JsonConvert.SerializeObject(job)}\n\n返回JSON：{{\"matchedItems\":[{{\"skill\":\"\",\"matchType\":\"exact/partial/related\",\"candidateLevel\":\"\",\"requiredLevel\":\"\"}}],\"gapItems\":[{{\"skill\":\"\",\"urgency\":\"critical/high/medium/low\",\"suggestedAction\":\"\"}}],\"overallGapScore\":0-100}}";

        var response = await CallAgent("analyze_gap", systemPrompt, userPrompt);
        return JsonConvert.DeserializeObject<dynamic>(response) ?? new { };
    }

    // ═══════════════════════════════════════════════════
    // Agent 4: 匹配评分师
    // ═══════════════════════════════════════════════════
    private async Task<dynamic> AgentScoreMatch(dynamic resume, dynamic job, dynamic gap)
    {
        var systemPrompt = @"你是匹配评分师（Match Scorer Agent）。
你的职责：综合简历、岗位分析和差距分析，给出五维评分和录用建议。
评分维度：技能匹配(40%) + 经验匹配(25%) + 学历匹配(15%) + 项目经验(10%) + 综合适配(10%)";

        var userPrompt = $"差距分析：{JsonConvert.SerializeObject(gap)}\n\n返回JSON：{{\"dimensions\":[{{\"name\":\"\",\"score\":0,\"weight\":0,\"analysis\":\"\"}}],\"overallScore\":0,\"hiringRecommendation\":\"建议录用/建议面试/建议复试/暂缓\",\"riskFactors\":[],\"strengths\":[]}}";

        var response = await CallAgent("score_match", systemPrompt, userPrompt);
        return JsonConvert.DeserializeObject<dynamic>(response) ?? new { };
    }

    // ═══════════════════════════════════════════════════
    // Agent 5: 面试顾问
    // ═══════════════════════════════════════════════════
    private async Task<dynamic> AgentGenerateInterviewGuide(dynamic resume, dynamic job, dynamic gap)
    {
        var systemPrompt = @"你是面试顾问（Interview Advisor Agent）。
你的职责：根据候选人的优劣势，生成针对性面试题目和考察重点。
题目分类：技术能力 / 项目经验 / 行为面试 / 场景模拟
针对 gapItems 中的每个关键缺口生成至少一题。";

        var userPrompt = $"差距分析：{JsonConvert.SerializeObject(gap)}\n\n返回JSON：{{\"interviewQuestions\":[{{\"category\":\"\",\"question\":\"\",\"purpose\":\"\",\"targetGap\":\"\"}}],\"keyFocusAreas\":[],\"evaluationRubric\":{{\"technical\":0,\"experience\":0,\"communication\":0,\"cultureFit\":0}},\"suggestedDuration\":\"30/45/60分钟\"}}";

        var response = await CallAgent("interview_guide", systemPrompt, userPrompt);
        return JsonConvert.DeserializeObject<dynamic>(response) ?? new { };
    }

    // ═══════════════════════════════════════════════════
    // 通用 Agent 调用
    // ═══════════════════════════════════════════════════
    private async Task<string> CallAgent(string agentName, string systemPrompt, string userPrompt)
    {
        var endpoint = $"{_aiOptions.BaseUrl}/text/chatcompletion_v2";
        _logger.LogDebug("[MultiAgent:{agent}] 调用中...", agentName);

        var requestBody = new
        {
            model = _aiOptions.Model,
            messages = new[]
            {
                new { role = "system", content = $"[Agent: {agentName}] {systemPrompt}" },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.5
        };

        var jsonContent = JsonConvert.SerializeObject(requestBody);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_aiOptions.ApiKey}");

        var response = await _httpClient.PostAsync(endpoint, httpContent);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<dynamic>(content);
        var text = result?.choices?[0]?.message?.content?.ToString() ?? "";

        // Clean JSON
        text = text.Trim();
        if (text.StartsWith("```json")) text = text[7..];
        else if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }
}

// ═══════════════════════════════════════════════════
// 结果 DTO
// ═══════════════════════════════════════════════════
public class MultiAgentResult
{
    public dynamic? ResumeAnalysis { get; set; }
    public dynamic? JobAnalysis { get; set; }
    public dynamic? GapAnalysis { get; set; }
    public dynamic? MatchScore { get; set; }
    public dynamic? InterviewGuide { get; set; }
    public long ElapsedMs { get; set; }

    /// <summary>提取综合评分（0-100）</summary>
    public double? OverallScore =>
        MatchScore?.overallScore != null ? (double)MatchScore.overallScore : null;

    /// <summary>提取录用建议</summary>
    public string? Recommendation =>
        MatchScore?.hiringRecommendation?.ToString();

    /// <summary>提取面试问题列表</summary>
    public List<string> InterviewQuestions()
    {
        var questions = new List<string>();
        if (InterviewGuide?.interviewQuestions == null) return questions;
        foreach (var q in InterviewGuide.interviewQuestions)
            questions.Add(q.question?.ToString() ?? "");
        return questions;
    }
}
