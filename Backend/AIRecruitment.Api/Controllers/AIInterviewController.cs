using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AIRecruitment.Api.Options;
using AIRecruitment.Api.Services;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/ai-interview")]
public class AIInterviewController : ControllerBase
{
    private readonly IAIInterviewService _interviewService;
    private readonly ILogger<AIInterviewController> _logger;
    private readonly AIOptions _aiOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _context;

    public AIInterviewController(
        IAIInterviewService interviewService,
        ILogger<AIInterviewController> logger,
        IOptions<AIOptions> aiOptions,
        IHttpClientFactory httpClientFactory,
        AppDbContext context)
    {
        _interviewService = interviewService;
        _logger = logger;
        _aiOptions = aiOptions.Value;
        _httpClientFactory = httpClientFactory;
        _context = context;
    }

    /// <summary>候选人：创建并开始面试会话</summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartInterview([FromBody] StartInterviewRequest request)
    {
        try
        {
            var session = await _interviewService.CreateSessionAsync(
                request.DeliveryId, request.CandidateId, request.JobId);

            var firstMessage = await _interviewService.StartInterviewAsync(session.SessionId);

            return Ok(new
            {
                code = 200,
                message = "面试已开始",
                data = new
                {
                    sessionId = session.SessionId,
                    firstMessage = new
                    {
                        firstMessage.MessageId,
                        firstMessage.Content,
                        firstMessage.CreatedAt
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始面试失败");
            return Ok(new { code = 500, message = "服务器内部错误" });
        }
    }

    /// <summary>候选人：回答问题</summary>
    [HttpPost("answer")]
    public async Task<IActionResult> Answer([FromBody] AnswerRequest request)
    {
        try
        {
            var message = await _interviewService.AnswerQuestionAsync(request.SessionId, request.Answer);

            // 判断面试是否结束
            var session = await _interviewService.GetSessionAsync(request.SessionId);
            var isEnded = session?.Status == 2 || session?.Status == 3;

            return Ok(new
            {
                code = 200,
                message = "回答已提交",
                data = new
                {
                    messageId = message.MessageId,
                    content = message.Content,
                    messageType = message.MessageType,
                    isEnded,
                    totalScore = isEnded ? session?.TotalScore : null,
                    scoresJson = isEnded ? session?.ScoresJson : null
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提交回答失败");
            return Ok(new { code = 500, message = "服务器内部错误" });
        }
    }

    /// <summary>候选人：结束面试</summary>
    [HttpPost("end")]
    public async Task<IActionResult> EndInterview([FromBody] EndInterviewRequest request)
    {
        try
        {
            var result = await _interviewService.EndInterviewAsync(request.SessionId);

            return Ok(new
            {
                code = 200,
                message = "面试已结束",
                data = new
                {
                    result.SessionId,
                    result.TotalScore,
                    result.ScoresJson,
                    result.TotalDuration
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束面试失败");
            return Ok(new { code = 500, message = "服务器内部错误" });
        }
    }

    /// <summary>候选人：获取面试结果</summary>
    [HttpGet("result/{sessionId}")]
    public async Task<IActionResult> GetResult(int sessionId)
    {
        try
        {
            var result = await _interviewService.GetInterviewResultAsync(sessionId);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取面试结果失败");
            return Ok(new { code = 500, message = "服务器内部错误" });
        }
    }

    /// <summary>候选人：获取会话状态</summary>
    [HttpGet("session/{sessionId}")]
    public async Task<IActionResult> GetSession(int sessionId)
    {
        try
        {
            var session = await _interviewService.GetSessionAsync(sessionId);
            if (session == null)
                return Ok(new { code = 404, message = "会话不存在" });

            return Ok(new
            {
                code = 200,
                data = new
                {
                    session.SessionId,
                    session.Status,
                    session.TotalScore,
                    session.ScoresJson,
                    session.TotalDuration,
                    session.StartTime,
                    session.EndTime,
                    JobTitle = session.Job?.Title,
                    CandidateName = session.Delivery?.Candidate?.RealName
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话失败");
            return Ok(new { code = 500, message = "服务器内部错误" });
        }
    }

    /// <summary>候选人：获取自己的AI面试记录</summary>
    [HttpGet("my-sessions")]
    public async Task<IActionResult> GetMySessions()
    {
        try
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int candidateId))
                return Ok(new { code = 401, message = "无法识别用户身份" });

            var sessions = await _context.AIInterviewSessions
                .Where(s => s.CandidateId == candidateId)
                .Include(s => s.Job)
                .Include(s => s.Delivery)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new
                {
                    s.SessionId,
                    s.DeliveryId,
                    s.Status,
                    s.TotalScore,
                    s.TotalDuration,
                    s.StartTime,
                    s.EndTime,
                    s.CreatedAt,
                    JobTitle = s.Job != null ? s.Job.Title : "",
                    MessageCount = s.Messages != null ? s.Messages.Count : 0
                })
                .ToListAsync();

            return Ok(new { code = 200, data = sessions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取候选人面试记录失败");
            return Ok(new { code = 500, message = "服务器内部错误" });
        }
    }

    /// <summary>HR端：获取AI面试记录列表（从JWT获取当前用户身份）</summary>
    [HttpGet("admin/list")]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null)
    {
        try
        {
            // 从 JWT Token 获取当前用户ID和角色
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (!int.TryParse(userIdStr, out int currentUserId))
                return Ok(new { code = 401, message = "无法识别用户身份" });

            // Admin 看全部，HR 只看自己的
            int? filterHrId = (role == "admin") ? null : currentUserId;

            var sessions = await _interviewService.GetAllSessionsAsync(filterHrId, page, pageSize, keyword);
            var data = sessions.Select(s => new
            {
                s.SessionId,
                s.Status,
                s.TotalScore,
                s.TotalDuration,
                s.StartTime,
                s.EndTime,
                s.CreatedAt,
                CandidateName = s.Delivery?.Candidate?.RealName,
                JobTitle = s.Job?.Title,
                MessageCount = s.Messages?.Count ?? 0
            });

            return Ok(new { code = 200, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取面试记录失败");
            return Ok(new { code = 500, message = "服务器内部错误" });
        }
    }

    /// <summary>HR端：获取面试对话详情</summary>
    [HttpGet("admin/messages/{sessionId}")]
    public async Task<IActionResult> GetMessages(int sessionId)
    {
        try
        {
            var messages = await _interviewService.GetSessionMessagesAsync(sessionId);
            var result = await _interviewService.GetInterviewResultAsync(sessionId);

            return Ok(new
            {
                code = 200,
                data = new
                {
                    result.SessionId,
                    result.TotalScore,
                    result.ScoresJson,
                    result.TotalDuration,
                    messages = result.Messages
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对话记录失败");
            return Ok(new { code = 500, message = "服务器内部错误" });
        }
    }
    /// <summary>候选人：语音转文字（MiniMax Speech 2.8 ASR）</summary>
    [HttpPost("speech-to-text")]
    public async Task<IActionResult> SpeechToText([FromBody] SpeechToTextRequest request)
    {
        try
        {
            var apiKey = _aiOptions.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
                return Ok(new { code = 500, message = "API Key 未配置" });

            byte[] audioBytes;
            try { audioBytes = Convert.FromBase64String(request.AudioBase64); }
            catch { return Ok(new { code = 400, message = "音频数据格式错误" }); }

            using var client = _httpClientFactory.CreateClient();
            using var content = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(audioBytes);
            audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/webm");
            content.Add(audioContent, "file", $"audio.{request.Format ?? "webm"}");
            content.Add(new StringContent("speech-02"), "model");

            using var requestMsg = new HttpRequestMessage(HttpMethod.Post,
                "https://api.minimax.chat/v1/audio/transcriptions");
            requestMsg.Headers.Add("Authorization", $"Bearer {apiKey}");
            requestMsg.Content = content;

            var response = await client.SendAsync(requestMsg);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("MiniMax STT 响应: {body}", responseBody);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonDocument.Parse(responseBody);
                var text = result.RootElement.TryGetProperty("text", out var textEl) ? textEl.GetString() ?? "" : "";
                return Ok(new { code = 200, data = new { text } });
            }
            else
            {
                _logger.LogWarning("MiniMax STT 失败: {status} {body}", response.StatusCode, responseBody);
                return Ok(new { code = 500, message = "语音识别失败，请重试" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "语音转文字失败");
            return Ok(new { code = 500, message = "语音转文字服务异常" });
        }
    }

    /// <summary>AI提问语音播报（MiniMax Speech 2.8 TTS）</summary>
    [HttpPost("text-to-speech")]
    public async Task<IActionResult> TextToSpeech([FromBody] TextToSpeechRequest request)
    {
        try
        {
            var apiKey = _aiOptions.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
                return Ok(new { code = 500, message = "API Key 未配置" });

            if (string.IsNullOrWhiteSpace(request.Text))
                return Ok(new { code = 400, message = "文本不能为空" });

            // 截断超长文本（TTS 通常有字符限制）
            var text = request.Text.Length > 500 ? request.Text[..500] : request.Text;
            var voiceId = request.VoiceId ?? "male-qn-qingse";  // MiniMax 男声：清澈

            // 调用 MiniMax TTS API (t2a_v2)
            var payload = new
            {
                model = "speech-02",
                text,
                stream = false,
                voice_setting = new
                {
                    voice_id = voiceId,
                    speed = 1.0,
                    vol = 1.0,
                    pitch = 0
                },
                audio_setting = new
                {
                    sample_rate = 32000,
                    bitrate = 128000,
                    format = "mp3",
                    channel = 1
                }
            };

            var jsonBody = JsonSerializer.Serialize(payload);
            using var httpContent = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

            using var client = _httpClientFactory.CreateClient();
            using var requestMsg = new HttpRequestMessage(HttpMethod.Post,
                "https://api.minimax.chat/v1/t2a_v2");
            requestMsg.Headers.Add("Authorization", $"Bearer {apiKey}");
            requestMsg.Content = httpContent;

            var response = await client.SendAsync(requestMsg);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("MiniMax TTS 响应(前200字): {body}",
                responseBody.Length > 200 ? responseBody[..200] : responseBody);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonDocument.Parse(responseBody);
                // MiniMax TTS 返回 data.audio 字段（hex mp3）
                var audioHex = "";
                if (result.RootElement.TryGetProperty("data", out var dataEl)
                    && dataEl.TryGetProperty("audio", out var audioEl))
                {
                    audioHex = audioEl.GetString() ?? "";
                }

                if (!string.IsNullOrEmpty(audioHex))
                {
                    return Ok(new
                    {
                        code = 200,
                        data = new
                        {
                            audioHex,
                            format = "mp3"
                        }
                    });
                }

                // 某些版本返回 audio_file
                var audioBase64 = "";
                if (result.RootElement.TryGetProperty("audio_file", out var afEl1))
                    audioBase64 = afEl1.GetString() ?? "";
                else if (result.RootElement.TryGetProperty("data", out var dataEl2)
                    && dataEl2.TryGetProperty("audio_file", out var afEl2))
                    audioBase64 = afEl2.GetString() ?? "";

                if (!string.IsNullOrEmpty(audioBase64))
                {
                    return Ok(new { code = 200, data = new { audioBase64, format = "mp3" } });
                }

                _logger.LogWarning("MiniMax TTS 返回体无音频字段: {body}", responseBody);
                return Ok(new { code = 500, message = "TTS返回异常，将使用浏览器降级播报" });
            }
            else
            {
                _logger.LogWarning("MiniMax TTS 失败: {status} {body}", response.StatusCode, responseBody);
                return Ok(new { code = 500, message = "TTS服务暂不可用，将使用浏览器降级播报" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文字转语音失败");
            return Ok(new { code = 500, message = "TTS服务异常" });
        }
    }

    /// <summary>纯语音模式：开始面试并返回 AI 第一问的语音</summary>
    [HttpPost("voice-start")]
    public async Task<IActionResult> VoiceStart([FromBody] StartInterviewRequest request)
    {
        try
        {
            var session = await _interviewService.CreateSessionAsync(
                request.DeliveryId, request.CandidateId, request.JobId);
            var firstMsg = await _interviewService.StartInterviewAsync(session.SessionId);

            var audioHex = await InternalTextToSpeechAsync(firstMsg.Content);
            return Ok(new
            {
                code = 200,
                data = new
                {
                    sessionId = session.SessionId,
                    audioHex,
                    isEnded = false
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "语音开始面试失败");
            return Ok(new { code = 500, message = "服务器内部错误" });
        }
    }

    /// <summary>纯语音模式：候选人说话 → AI 语音回复（全程无文字）</summary>
    [HttpPost("voice-answer")]
    public async Task<IActionResult> VoiceAnswer([FromBody] VoiceAnswerRequest request)
    {
        try
        {
            // 1. 语音转文字
            var text = await InternalSpeechToTextAsync(request.AudioBase64, request.Format);
            if (string.IsNullOrWhiteSpace(text))
                return Ok(new { code = 400, message = "未识别到语音内容" });

            // 2. AI 处理回答
            var message = await _interviewService.AnswerQuestionAsync(request.SessionId, text);
            var session = await _interviewService.GetSessionAsync(request.SessionId);
            var isEnded = session?.Status == 2 || session?.Status == 3;

            // 3. 文字转语音（AI 回复）
            var audioHex = await InternalTextToSpeechAsync(message.Content);

            return Ok(new
            {
                code = 200,
                data = new
                {
                    audioHex,
                    isEnded,
                    totalScore = isEnded ? session?.TotalScore : null,
                    scoresJson = isEnded ? session?.ScoresJson : null
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "语音回答处理失败");
            return Ok(new { code = 500, message = "服务器内部错误" });
        }
    }

    // ─── 内部方法：STT/TTS 避免代码重复 ───

    private async Task<string> InternalSpeechToTextAsync(string audioBase64, string? format)
    {
        var apiKey = _aiOptions.ApiKey;
        if (string.IsNullOrEmpty(apiKey)) throw new Exception("API Key 未配置");

        var pureBase64 = audioBase64.Contains(',') ? audioBase64.Split(',')[1] : audioBase64;
        byte[] audioBytes = Convert.FromBase64String(pureBase64);

        using var client = _httpClientFactory.CreateClient();
        using var content = new MultipartFormDataContent();
        var audioContent = new ByteArrayContent(audioBytes);
        audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/webm");
        content.Add(audioContent, "file", $"audio.{format ?? "webm"}");
        content.Add(new StringContent("speech-02"), "model");

        using var requestMsg = new HttpRequestMessage(HttpMethod.Post, "https://api.minimax.chat/v1/audio/transcriptions");
        requestMsg.Headers.Add("Authorization", $"Bearer {apiKey}");
        requestMsg.Content = content;

        var response = await client.SendAsync(requestMsg);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var result = JsonDocument.Parse(responseBody);
            return result.RootElement.TryGetProperty("text", out var textEl) ? textEl.GetString() ?? "" : "";
        }
        throw new Exception("语音识别失败");
    }

    private async Task<string> InternalTextToSpeechAsync(string text, string voiceId = "male-qn-qingse")
    {
        var apiKey = _aiOptions.ApiKey;
        if (string.IsNullOrEmpty(apiKey)) throw new Exception("API Key 未配置");

        var trimmed = text.Length > 500 ? text[..500] : text;

        var payload = new
        {
            model = "speech-02",
            text = trimmed,
            stream = false,
            voice_setting = new { voice_id = voiceId, speed = 1.0, vol = 1.0, pitch = 0 },
            audio_setting = new { sample_rate = 32000, bitrate = 128000, format = "mp3", channel = 1 }
        };

        var jsonBody = JsonSerializer.Serialize(payload);
        using var client = _httpClientFactory.CreateClient();
        using var httpContent = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
        using var requestMsg = new HttpRequestMessage(HttpMethod.Post, "https://api.minimax.chat/v1/t2a_v2");
        requestMsg.Headers.Add("Authorization", $"Bearer {apiKey}");
        requestMsg.Content = httpContent;

        var response = await client.SendAsync(requestMsg);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var result = JsonDocument.Parse(responseBody);
            if (result.RootElement.TryGetProperty("data", out var dataEl) && dataEl.TryGetProperty("audio", out var audioEl))
                return audioEl.GetString() ?? "";
            if (result.RootElement.TryGetProperty("audio_file", out var afEl))
                return afEl.GetString() ?? "";
        }
        throw new Exception("TTS生成失败");
    }
}

public record StartInterviewRequest(
    [property: JsonPropertyName("deliveryId")] int DeliveryId,
    [property: JsonPropertyName("candidateId")] int CandidateId,
    [property: JsonPropertyName("jobId")] int JobId);

public record AnswerRequest(
    [property: JsonPropertyName("sessionId")] int SessionId,
    [property: JsonPropertyName("answer")] string Answer);

public record EndInterviewRequest(
    [property: JsonPropertyName("sessionId")] int SessionId);

public record SpeechToTextRequest(
    [property: JsonPropertyName("audioBase64")] string AudioBase64,
    [property: JsonPropertyName("format")] string? Format);

public record TextToSpeechRequest(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("voiceId")] string? VoiceId);

public record VoiceAnswerRequest(
    [property: JsonPropertyName("sessionId")] int SessionId,
    [property: JsonPropertyName("audioBase64")] string AudioBase64,
    [property: JsonPropertyName("format")] string? Format);
