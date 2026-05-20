using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// [Authorize]
public class AIController : ControllerBase
{
    private readonly IAIService _aiService;

    public AIController(IAIService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("resume/analyze")]
    // [Authorize(Roles = "hr")]
    public async Task<IActionResult> AnalyzeResume([FromBody] AnalyzeRequest request)
    {
        var result = await _aiService.AnalyzeResumeAsync(request.DeliveryId);
        return Ok(new { code = 200, message = "AI解析完成", data = result });
    }

    [HttpGet("resume/score")]
    // [Authorize(Roles = "hr")]
    public async Task<IActionResult> ScoreResume([FromQuery] int deliveryId)
    {
        var result = await _aiService.ScoreResumeAsync(deliveryId);
        return Ok(new { code = 200, data = result });
    }

    [HttpGet("interview/generate")]
    // [Authorize(Roles = "hr")]
    public async Task<IActionResult> GenerateQuestions([FromQuery] int deliveryId)
    {
        var result = await _aiService.GenerateInterviewQuestionsAsync(deliveryId);
        return Ok(new { code = 200, data = result });
    }

    [HttpGet("insights")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> GetRecruitmentInsights([FromQuery] int hrId, [FromQuery] string period = "week")
    {
        var result = await _aiService.GetRecruitmentInsightsAsync(hrId, period);
        return Ok(new { code = 200, data = result });
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentAnalyses([FromQuery] int limit = 10)
    {
        var result = await _aiService.GetRecentAnalysesAsync(limit);
        return Ok(new { code = 200, data = result });
    }
}

public record AnalyzeRequest(int DeliveryId);
