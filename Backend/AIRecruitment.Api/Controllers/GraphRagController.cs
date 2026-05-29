using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

/// <summary>GraphRAG API — 从 GraphController 拆分</summary>
[ApiController]
[Route("api/graph")]
public class GraphRagController : ControllerBase
{
    private readonly GraphRAGService _rag;

    public GraphRagController(GraphRAGService rag)
    {
        _rag = rag;
    }

    /// <summary>GraphRAG 社区检测 — 对标微软 GraphRAG</summary>
    [HttpGet("rag/communities")]
    public async Task<IActionResult> GetRagCommunities()
    {
        try
        {
            var report = await _rag.DetectCommunitiesAsync();
            return Ok(new { code = 200, data = report });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    /// <summary>GraphRAG 全局洞察问答</summary>
    [HttpPost("rag/insight")]
    public async Task<IActionResult> GetRagInsight([FromBody] NLQueryRequest request)
    {
        try
        {
            var answer = await _rag.GenerateGlobalInsightAsync(request.Question);
            return Ok(new { code = 200, data = new { question = request.Question, answer } });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }
}
