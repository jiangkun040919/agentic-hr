using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Options;
using System.Text;

namespace AIRecruitment.Api.Services;

public interface IAIInterviewService
{
    Task<AIInterviewSession> CreateSessionAsync(int deliveryId, int candidateId, int jobId);
    Task<AIInterviewMessage> StartInterviewAsync(int sessionId);
    Task<AIInterviewMessage> AnswerQuestionAsync(int sessionId, string answer);
    Task<AIInterviewResult> GetInterviewResultAsync(int sessionId);
    Task<AIInterviewSession?> GetSessionAsync(int sessionId);
    Task<AIInterviewResult> EndInterviewAsync(int sessionId);
    Task<List<AIInterviewSession>> GetAllSessionsAsync(int? hrId, int page, int pageSize, string? keyword);
    Task<List<AIInterviewMessage>> GetSessionMessagesAsync(int sessionId);
}

public class AIInterviewResult
{
    public int SessionId { get; set; }
    public int? TotalScore { get; set; }
    public string? ScoresJson { get; set; }
    public string? TranscriptJson { get; set; }
    public int TotalDuration { get; set; }
    public int Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? JobTitle { get; set; }
    public string? CandidateName { get; set; }
    public List<AIInterviewMessageDto> Messages { get; set; } = new();
}

public class AIInterviewMessageDto
{
    public int MessageId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public int? Score { get; set; }
    public string? Evaluation { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AIInterviewService : IAIInterviewService
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly AIOptions _aiOptions;
    private readonly ISignalRService? _signalR;
    private readonly ILogger<AIInterviewService> _logger;

    private const int MIN_ROUNDS = 3;
    private const int MAX_ROUNDS = 20;

    public AIInterviewService(
        AppDbContext context,
        IOptions<AIOptions> aiOptions,
        ISignalRService? signalR,
        ILogger<AIInterviewService> logger)
    {
        _context = context;
        _httpClient = new HttpClient();
        _signalR = signalR;
        _aiOptions = aiOptions.Value;
        _logger = logger;
    }

    public async Task<AIInterviewSession> CreateSessionAsync(int deliveryId, int candidateId, int jobId)
    {
        // 检查是否允许进行AI面试
        var delivery = await _context.Deliveries
            .Include(d => d.Candidate)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId);
        if (delivery == null)
            throw new Exception("投递记录不存在");
        
        if (!delivery.AllowAIInterview)
            throw new Exception("HR尚未允许您参加AI面试，请等待通知");
        
        // 检查截止时间
        if (delivery.AIInterviewDeadline.HasValue && DateTime.Now > delivery.AIInterviewDeadline.Value)
            throw new Exception("AI面试申请已过期");

        // 检查是否已完成过面试（禁止重复面试）
        var completed = await _context.AIInterviewSessions
            .FirstOrDefaultAsync(s => s.DeliveryId == deliveryId && (s.Status == 2 || s.Status == 3));
        if (completed != null)
            throw new Exception("您已完成该岗位的AI面试，不可重复面试");

        var existing = await _context.AIInterviewSessions
            .FirstOrDefaultAsync(s => s.DeliveryId == deliveryId && s.Status == 1);

        if (existing != null)
            return existing;

        // 从 Delivery 取 CandidateId（避免前端传错导致外键冲突）
        var actualCandidateId = delivery.CandidateId;
        var actualJobId = delivery.JobId;

        var session = new AIInterviewSession
        {
            DeliveryId = deliveryId,
            CandidateId = actualCandidateId,
            JobId = actualJobId,
            Status = 0,
            CreatedAt = DateTime.Now
        };

        _context.AIInterviewSessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<AIInterviewMessage> StartInterviewAsync(int sessionId)
    {
        var session = await _context.AIInterviewSessions
            .Include(s => s.Job)
            .Include(s => s.Delivery).ThenInclude(d => d!.Candidate)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session == null) throw new Exception("面试会话不存在");

        var job = session.Job!;
        var candidate = session.Delivery?.Candidate;

        session.Status = 1;
        session.StartTime = DateTime.Now;
        await _context.SaveChangesAsync();

        var systemPrompt = $@"你是一个专业、严谨的AI面试官。请严格按照以下规则进行面试：

## 面试基本信息
- 岗位：【{job.Title}】
- 岗位职责：{job.JD}
- 任职要求：{job.Requirements}

## 面试规则
1. 至少{MIN_ROUNDS}轮问答，最多{MAX_ROUNDS}轮
2. 每轮只问1个问题，具体且针对岗位
3. 问题类型多样化：技术深度 → 项目经验 → 场景模拟 → 行为面试
4. 保持专业、友好，不给提示或透露答案
5. 只输出问题，不加'面试官：'等前缀

## 岗位类型题库策略
{(job.Title.Contains("Java") || job.Title.Contains("Python") || job.Title.Contains("前端") || job.Title.Contains("Go") || job.Title.Contains("C++") || job.Title.Contains("Rust") || job.Title.Contains("开发") || job.Title.Contains("工程师") || job.Title.Contains("架构") || job.Title.Contains("DevOps") ? 
"- 技术岗策略：从基础原理开始，逐步深入到系统设计和架构决策" : "")}
{(job.Title.Contains("产品") || job.Title.Contains("经理") ? 
"- 产品岗策略：侧重需求分析能力、数据驱动思维、跨团队协作案例" : "")}
{(job.Title.Contains("数据") || job.Title.Contains("分析") || job.Title.Contains("AI") || job.Title.Contains("机器") ? 
"- 数据/AI岗策略：侧重分析方法论、模型选型经验、业务理解能力" : "")}

## Few-shot 追问示例
候选人回答较浅时追问细节，回答完整时深入复盘反思。

## 评分校准锚点
90-100：表现卓越，技能高度匹配，沟通清晰有深度
75-89：表现良好，满足岗位核心要求，有明显亮点
60-74：基本胜任，但有短板需进一步考察
40-59：存在较大差距，不推荐
0-39：与岗位严重不匹配

当判断候选人回答质量足够评估或超过{MAX_ROUNDS}轮时，返回JSON评分结束面试：
{{""totalScore"":数字,""professional"":数字,""communication"":数字,""problemSolving"":数字,""cultureFit"":数字,""strengths"":[""优势""],""weaknesses"":[""不足""],""recommendation"":""建议/不建议/待定""}}

";

        var userPrompt = candidate != null
            ? $"候选人信息：姓名={candidate.RealName}，学历={candidate.Education ?? "未知"}，工作年限={candidate.WorkYears ?? 0}年。\n\n请以面试官身份开始面试，第一句话直接让候选人做自我介绍，不要说其他内容。"
            : "请以面试官身份开始面试，第一句话直接让候选人做自我介绍，不要说其他内容。";

        var content = await CallAIAsync(systemPrompt, userPrompt);

        var message = new AIInterviewMessage
        {
            SessionId = sessionId,
            Role = "ai",
            Content = content,
            MessageType = "question",
            OrderIndex = 1,
            CreatedAt = DateTime.Now
        };

        _context.AIInterviewMessages.Add(message);
        await _context.SaveChangesAsync();

        return message;
    }

    public async Task<AIInterviewMessage> AnswerQuestionAsync(int sessionId, string answer)
    {
        var session = await _context.AIInterviewSessions
            .Include(s => s.Job)
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session == null) throw new Exception("面试会话不存在");
        if (session.Status != 1) throw new Exception("面试未进行中");

        var messageHistory = session.Messages!.OrderBy(m => m.OrderIndex).ToList();
        var currentRound = messageHistory.Count(m => m.Role == "ai") + 1;

        var answerMsg = new AIInterviewMessage
        {
            SessionId = sessionId,
            Role = "candidate",
            Content = answer,
            MessageType = "answer",
            OrderIndex = messageHistory.Count + 1,
            CreatedAt = DateTime.Now
        };
        _context.AIInterviewMessages.Add(answerMsg);

        string content;
        bool isEnded = false;

        // 让 AI 自行判断是继续追问还是结束面试
        var (responseContent, shouldEnd) = await GenerateAIResponseAsync(session, messageHistory, answer, currentRound);
        content = responseContent;
        isEnded = shouldEnd;

        if (isEnded)
        {
            session.Status = 2;
            session.EndTime = DateTime.Now;
            session.TotalDuration = (int)(session.EndTime.Value - session.StartTime!.Value).TotalSeconds;

            // 同步投递状态
            if (session.DeliveryId > 0)
            {
                var delivery = await _context.Deliveries.FindAsync(session.DeliveryId);
                if (delivery != null && delivery.Status < 2)
                {
                    delivery.Status = 2;
                    delivery.UpdateTime = DateTime.Now;
                }
                // 发通知给 HR
                if (delivery != null)
                {
                    var notification = new Notification
                    {
                        UserId = delivery.HrId,
                        Type = "interview",
                        Title = "AI 面试已完成",
                        Content = $"候选人完成了一场AI面试，得分 {session.TotalScore ?? 0} 分，" +
                                  $"投递ID: {session.DeliveryId}，请查看详情。",
                        RelatedId = sessionId,
                        RelatedType = "ai_interview",
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(notification);
                    // 实时推送通知给 HR
                    if (_signalR != null)
                        await _signalR.SendToUserAsync(delivery.HrId, "NewNotification", new
                        {
                            notification.Title,
                            notification.Content,
                            notification.Type,
                            notification.CreatedAt
                        });
                }
            }
        }

        var aiMsg = new AIInterviewMessage
        {
            SessionId = sessionId,
            Role = "ai",
            Content = content,
            MessageType = isEnded ? "evaluation" : "question",
            OrderIndex = messageHistory.Count + 2,
            CreatedAt = DateTime.Now
        };
        _context.AIInterviewMessages.Add(aiMsg);
        await _context.SaveChangesAsync();

        return aiMsg;
    }

    /// <summary>候选人手动结束面试</summary>

    /// <summary>
    /// AI 自主判断：继续追问 or 结束面试出分
    /// 返回 (回复内容, 是否结束面试)
    /// </summary>
    private async Task<(string content, bool shouldEnd)> GenerateAIResponseAsync(
        AIInterviewSession session,
        List<AIInterviewMessage> history,
        string lastAnswer,
        int currentRound)
    {
        var job = session.Job!;

        // 如果超过安全上限，强制结束
        if (currentRound >= MAX_ROUNDS)
        {
            var evalContent = await GenerateFinalEvaluationAsync(session, history, lastAnswer);
            return (evalContent, true);
        }

        var historySummary = string.Join("\n", history.Select(m =>
            $"[{m.Role}]: {m.Content}"));

        // 构建一个让 AI 自行判断是否结束的 prompt
        var systemPrompt = $@"你是一个专业面试官，正在进行【{job.Title}】岗位的面试。\n当前是第{currentRound + 1}轮问答。\n\n请根据候选人最新的回答，自行判断：\n- 如果候选人的回答已经足够充分，可以进行综合评估了，或者回答质量很差已经没有必要继续了，请输出最终的JSON评分结果来结束面试\n- 如果还需要继续深入了解候选人的能力，请给出简短点评（20字内）然后问下一个问题\n\n面试结束的JSON评分格式要求：\n{{\""totalScore\"":数字,\""professional\"":数字,\""communication\"":数字,\""problemSolving\"":数字,\""cultureFit\"":数字,\""strengths\"":[\""优势1\"",\""优势2\""],\""weaknesses\"":[\""不足1\""],\""recommendation\"":\""建议/不建议/待定\""}}\n\n如果选择继续面试，只输出点评和下一个问题，不要加任何前缀或格式。\n如果选择结束面试，只输出JSON，不要加markdown代码块或其他文字。";

        var userPrompt = $@"面试历史：\n{historySummary}\n\n候选人最新回答：{lastAnswer}\n\n当前第{currentRound + 1}轮。请判断是继续提问还是结束面试给出评分。";

        var response = await CallAIAsync(systemPrompt, userPrompt);
        response = response.Trim();

        // 判断 AI 是否返回了 JSON 评分（表示面试结束）
        if (IsEvaluationJson(response))
        {
            // 解析评分并保存
            ParseAndSaveScores(session, response, history);

            // 只返回评分结果，不附加多余文字
            return ($"【面试结束】\n\n{response}", true);
        }
        else
        {
            // AI 选择继续提问
            return (response, false);
        }
    }

    /// <summary>
    /// 判断 AI 返回的内容是否为评分 JSON
    /// </summary>
    private bool IsEvaluationJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        var trimmed = content.TrimStart();
        // 检查是否以 { 开头，包含 totalScore 字段
        return trimmed.StartsWith("{") && content.Contains("totalScore");
    }

    private async Task<string> GenerateFinalEvaluationAsync(
        AIInterviewSession session,
        List<AIInterviewMessage> history,
        string lastAnswer)
    {
        var job = session.Job!;

        var qaPairs = history
            .Where(m => m.Role == "candidate")
            .Select((m, i) =>
            {
                var q = i == 0 ? history[0].Content : history.Skip(i * 2 - 1).FirstOrDefault()?.Content ?? "";
                return $"Q{i + 1}: {q}\nA{i + 1}: {m.Content}";
            })
            .ToList();

        var lastQuestion = history.LastOrDefault(m => m.Role == "ai")?.Content ?? "";
        qaPairs.Add($"Q{qaPairs.Count + 1}: {lastQuestion}\nA{qaPairs.Count + 1}: {lastAnswer}");

        var systemPrompt = $@"你是一个资深HR面试专家。请根据候选人完整的面试回答，给出综合评价。\n\n评价必须包含：\n1. 综合评分(0-100整数)\n2. 分项评分：professional(专业能力)、communication(沟通表达)、problemSolving(问题解决)、cultureFit(文化适配)，每项0-100\n3. 优势(数组，2-3条)\n4. 不足(数组，1-2条)\n5. 录用建议：建议/不建议/待定\n\n请返回纯JSON格式，不要markdown代码块，不要任何其他文字。格式如下：\n{{\""totalScore\"":数字,\""professional\"":数字,\""communication\"":数字,\""problemSolving\"":数字,\""cultureFit\"":数字,\""strengths\"":[\""优势1\""],\""weaknesses\"":[\""不足1\""],\""recommendation\"":\""建议\""}}";

        var userPrompt = $@"岗位：{job.Title}\n要求：{job.Requirements}\n\n问答记录：\n{string.Join("\n", qaPairs)}";

        var eval = await CallAIAsync(systemPrompt, userPrompt);
        eval = eval.Trim();
        if (eval.StartsWith("```json")) eval = eval.Substring(7);
        else if (eval.StartsWith("```")) eval = eval.Substring(3);
        if (eval.EndsWith("```")) eval = eval.Substring(0, eval.Length - 3);
        eval = eval.Trim();

        ParseAndSaveScores(session, eval, history);

        // 直接返回评分 JSON，不附加多余文字
        return eval;
    }

    /// <summary>
    /// 解析评分 JSON 并保存到 Session
    /// </summary>
    private void ParseAndSaveScores(AIInterviewSession session, string evalJson, List<AIInterviewMessage> history)
    {
        try
        {
            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(evalJson);
            if (parsed?.totalScore != null)
            {
                session.TotalScore = (int)parsed.totalScore;
                session.ScoresJson = Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    professional = (int?)parsed.professional ?? 70,
                    communication = (int?)parsed.communication ?? 70,
                    problemSolving = (int?)parsed.problemSolving ?? 70,
                    cultureFit = (int?)parsed.cultureFit ?? 70
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"解析评价结果失败: {ex.Message}");
            session.TotalScore = 70;
            session.ScoresJson = "{\"professional\":70,\"communication\":70,\"problemSolving\":70,\"cultureFit\":70}";
        }

        session.TranscriptJson = Newtonsoft.Json.JsonConvert.SerializeObject(history.Select(m => new
        {
            m.Role,
            m.Content,
            m.MessageType
        }));
    }

    public async Task<AIInterviewResult> EndInterviewAsync(int sessionId)
    {
        var session = await _context.AIInterviewSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session == null) throw new Exception("面试会话不存在");

        if (session.Status == 1)
        {
            session.Status = 3;
            session.EndTime = DateTime.Now;
            if (session.StartTime.HasValue)
                session.TotalDuration = (int)(session.EndTime.Value - session.StartTime.Value).TotalSeconds;
        }

        // 同步更新投递状态
        if (session.DeliveryId > 0)
        {
            var delivery = await _context.Deliveries.FindAsync(session.DeliveryId);
            if (delivery != null && delivery.Status < 2)
            {
                delivery.Status = 2; // 标记为面试中→已完成
                delivery.UpdateTime = DateTime.Now;
            }
        }

        // 发送通知给 HR
        if (session.DeliveryId > 0)
        {
            var delivery = await _context.Deliveries
                .Include(d => d.Job)
                .FirstOrDefaultAsync(d => d.DeliveryId == session.DeliveryId);
            if (delivery != null)
            {
                var notification = new Notification
                {
                    UserId = delivery.HrId,
                    Type = "interview",
                    Title = "AI 面试已完成",
                    Content = $"候选人 {(delivery.Candidate != null ? delivery.Candidate.RealName : "未知")} " +
                              $"完成了 {delivery.Job?.Title ?? "岗位"} 的AI面试，得分 {session.TotalScore ?? 0} 分，请查看详情。",
                    RelatedId = sessionId,
                    RelatedType = "ai_interview",
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notification);
                if (_signalR != null)
                    await _signalR.SendToUserAsync(delivery.HrId, "NewNotification", new
                    {
                        notification.Title,
                        notification.Content,
                        notification.Type,
                        notification.CreatedAt
                    });
            }
        }

        await _context.SaveChangesAsync();

        return await GetInterviewResultAsync(sessionId);
    }

    public async Task<AIInterviewResult> GetInterviewResultAsync(int sessionId)
    {
        var session = await _context.AIInterviewSessions
            .Include(s => s.Messages)
            .Include(s => s.Job)
            .Include(s => s.Delivery).ThenInclude(d => d!.Candidate)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session == null) throw new Exception("面试会话不存在");

        return new AIInterviewResult
        {
            SessionId = session.SessionId,
            TotalScore = session.TotalScore,
            ScoresJson = session.ScoresJson,
            TranscriptJson = session.TranscriptJson,
            TotalDuration = session.TotalDuration,
            Status = session.Status,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            JobTitle = session.Job?.Title,
            CandidateName = session.Delivery?.Candidate?.RealName,
            Messages = session.Messages!
                .OrderBy(m => m.OrderIndex)
                .Select(m => new AIInterviewMessageDto
                {
                    MessageId = m.MessageId,
                    Role = m.Role,
                    Content = m.Content,
                    MessageType = m.MessageType,
                    Score = m.Score,
                    Evaluation = m.Evaluation,
                    CreatedAt = m.CreatedAt
                }).ToList()
        };
    }

    public async Task<AIInterviewSession?> GetSessionAsync(int sessionId)
    {
        return await _context.AIInterviewSessions
            .Include(s => s.Job)
            .Include(s => s.Delivery).ThenInclude(d => d!.Candidate)
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
    }

    public async Task<List<AIInterviewSession>> GetAllSessionsAsync(int? hrId, int page, int pageSize, string? keyword)
    {
        var query = _context.AIInterviewSessions
            .Include(s => s.Delivery).ThenInclude(d => d!.Candidate)
            .Include(s => s.Job)
            .Where(s => s.Delivery != null && s.Job != null);

        // HR 只能看自己的岗位投递记录，Admin 看全部
        if (hrId.HasValue)
        {
            query = query.Where(s => s.Delivery!.HrId == hrId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(s =>
                s.Delivery!.Candidate!.RealName.Contains(keyword) ||
                s.Job!.Title.Contains(keyword));

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<AIInterviewMessage>> GetSessionMessagesAsync(int sessionId)
    {
        return await _context.AIInterviewMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.OrderIndex)
            .ToListAsync();
    }

    private async Task<string> CallAIAsync(string systemPrompt, string userPrompt)
    {
        // 模拟模式：返回预设回答，无需调用真实 AI API
        if (_aiOptions.UseMock)
        {
            return await GenerateMockResponse(systemPrompt, userPrompt);
        }

        if (string.IsNullOrEmpty(_aiOptions.ApiKey))
            throw new Exception("AI API Key未配置");

        var endpoint = $"{_aiOptions.BaseUrl}/chat/completions";
        var model = _aiOptions.Model;

        _logger.LogInformation($"调用AI API: {endpoint}, 模型: {model}");

        var requestBody = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.7,
            max_tokens = 1000
        };

        var jsonContent = JsonConvert.SerializeObject(requestBody);
        _logger.LogInformation($"请求体: {jsonContent}");

        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // 清除之前的 Header
        _httpClient.DefaultRequestHeaders.Clear();
        
        // MiniMax API 认证格式: Authorization: Bearer ***
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_aiOptions.ApiKey}");
        
        // 添加 Content-Type Header
        httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        try
        {
            _logger.LogInformation($"发送请求到: {endpoint}");
            var response = await _httpClient.PostAsync(endpoint, httpContent);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"AI API 响应状态码: {response.StatusCode}");
            _logger.LogInformation($"AI API 响应内容: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"AI API错误: {response.StatusCode} - {responseBody}");
                throw new Exception($"AI服务调用失败({response.StatusCode}): {responseBody}");
            }

            var result = JsonConvert.DeserializeObject<dynamic>(responseBody);
            var content = result?.choices?[0]?.message?.content?.ToString() ?? "";

            // 过滤 MiniMax M2.7 的 <think...</think 思考标签
            content = System.Text.RegularExpressions.Regex.Replace(content, @"<think[^>]*>.*?</think\s*>", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            content = System.Text.RegularExpressions.Regex.Replace(content, @"<think[^>]*>.*", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            content = content.Trim();

            _logger.LogInformation($"AI 返回内容（过滤后）: {content}");
            return content;
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError($"HTTP请求异常: {httpEx.Message}");
            throw new Exception($"AI服务网络连接失败，请检查网络或API地址。详细错误: {httpEx.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"调用AI服务异常: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 模拟 AI 回答，用于测试（无需真实 API Key）
    /// </summary>
    private async Task<string> GenerateMockResponse(string systemPrompt, string userPrompt)
    {
        await Task.Delay(500);

        // 根据 userPrompt 内容判断返回哪种模拟回答
        if (userPrompt.Contains("自我介绍") || userPrompt.Contains("开始面试"))
        {
            return "你好！欢迎参加本次AI面试，请你先做一个简单的自我介绍。";
        }

        if (userPrompt.Contains("问答记录") || userPrompt.Contains("综合评价") || userPrompt.Contains("总结"))
        {
            // 模拟最终评价 JSON
            return @"{""totalScore"":82,""professional"":85,""communication"":80,""problemSolving"":78,""cultureFit"":85,""strengths"":[""专业基础扎实"",""沟通能力良好"",""学习意愿强""],""weaknesses"":[""部分细节经验略显不足""],""recommendation"":""建议""}";
        }

        // 模拟后续追问
        var followUps = new[]
        {
            "感谢你的回答。接下来我想了解一下，你在以往项目中遇到的最大挑战是什么？你是如何解决的？",
            "很好。如果让你独立负责一个新项目，你会如何规划前三个月的工作重点？",
            "我注意到你的经历里有团队协作的经验。请举一个具体例子，说明你是如何处理团队内部意见分歧的？",
            "你为什么选择应聘我们这个岗位？你认为自己最大的优势是什么？"
        };

        var hash = userPrompt.GetHashCode() & 0x7FFFFFFF;
        return followUps[hash % followUps.Length];
    }
}
