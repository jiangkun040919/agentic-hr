using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

/// <summary>候选人端智能 API — 从 GraphController 拆分</summary>
[ApiController]
[Route("api/graph")]
public class CandidateController : ControllerBase
{
    private readonly CandidateIntelligenceService _candidateIntel;

    public CandidateController(CandidateIntelligenceService candidateIntel)
    {
        _candidateIntel = candidateIntel;
    }

    /// <summary>成长路径规划</summary>
    [HttpGet("candidate/career-path")]
    public async Task<IActionResult> CareerPath([FromQuery] int candidateId, [FromQuery] int jobId)
    {
        try
        {
            var result = await _candidateIntel.GetCareerPathAsync(candidateId, jobId);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>推荐岗位</summary>
    [HttpGet("candidate/recommend-jobs")]
    public async Task<IActionResult> RecommendJobs([FromQuery] int candidateId)
    {
        try
        {
            var result = await _candidateIntel.RecommendCareerPathsAsync(candidateId);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>竞争力分析</summary>
    [HttpGet("candidate/competitiveness")]
    public async Task<IActionResult> Competitiveness([FromQuery] int deliveryId)
    {
        try
        {
            var result = await _candidateIntel.AnalyzeCompetitivenessAsync(deliveryId);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>透明匹配报告</summary>
    [HttpGet("candidate/transparent-match")]
    public async Task<IActionResult> TransparentMatch([FromQuery] int candidateId, [FromQuery] int jobId)
    {
        try
        {
            var result = await _candidateIntel.GetTransparentMatchAsync(candidateId, jobId);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }
}
