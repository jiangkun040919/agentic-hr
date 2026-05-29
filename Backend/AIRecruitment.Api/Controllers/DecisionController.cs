using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

/// <summary>招聘决策智能 API — 从 GraphController 拆分</summary>
[ApiController]
[Route("api/graph")]
public class DecisionController : ControllerBase
{
    private readonly DecisionIntelligenceService _decision;

    public DecisionController(DecisionIntelligenceService decision)
    {
        _decision = decision;
    }

    /// <summary>可解释匹配报告（图谱证据链）</summary>
    [HttpGet("decision/explain-match")]
    public async Task<IActionResult> ExplainMatch([FromQuery] int candidateId, [FromQuery] int jobId)
    {
        try
        {
            var report = await _decision.ExplainMatchAsync(candidateId, jobId);
            return Ok(new { code = 200, data = report });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>What-if 推演：学一个技能后匹配分变化</summary>
    [HttpGet("decision/what-if")]
    public async Task<IActionResult> WhatIf([FromQuery] int candidateId, [FromQuery] int jobId, [FromQuery] string newSkill)
    {
        try
        {
            var result = await _decision.WhatIfAsync(candidateId, jobId, newSkill);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>批量 What-if：找出最优学习路径</summary>
    [HttpPost("decision/what-if-batch")]
    public async Task<IActionResult> WhatIfBatch([FromBody] WhatIfBatchRequest request)
    {
        try
        {
            var results = await _decision.WhatIfBatchAsync(request.CandidateId, request.JobId, request.Skills);
            return Ok(new { code = 200, data = results });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>五维录用风险雷达</summary>
    [HttpGet("decision/risk-radar")]
    public async Task<IActionResult> RiskRadar([FromQuery] int candidateId, [FromQuery] int jobId)
    {
        try
        {
            var radar = await _decision.AnalyzeHiringRiskAsync(candidateId, jobId);
            return Ok(new { code = 200, data = radar });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }
}

public record WhatIfBatchRequest(int CandidateId, int JobId, List<string> Skills);
