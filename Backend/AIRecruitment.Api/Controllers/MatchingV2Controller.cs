using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

/// <summary>
/// V2 增强匹配控制器 — 三通道融合（规则 + ML + 多智能体 + Graph RAG）
/// </summary>
[ApiController]
[Route("api/v2/matching")]
[Authorize]
public class MatchingV2Controller : ControllerBase
{
    private readonly EnhancedMatchingService _matching;
    private readonly MLMatchingService _ml;
    private readonly MultiAgentMatchingService _multiAgent;
    private readonly GraphRAGService _graphRAG;

    public MatchingV2Controller(
        EnhancedMatchingService matching,
        MLMatchingService ml,
        MultiAgentMatchingService multiAgent,
        GraphRAGService graphRAG)
    {
        _matching = matching;
        _ml = ml;
        _multiAgent = multiAgent;
        _graphRAG = graphRAG;
    }

    /// <summary>V2 三通道融合匹配</summary>
    [HttpPost("match/{jobId}")]
    public async Task<IActionResult> MatchV2(int jobId, [FromBody] MatchV2Request request)
    {
        if (string.IsNullOrWhiteSpace(request.ResumeText))
            return BadRequest(new { error = "简历文本不能为空" });

        var result = await _matching.MatchV2Async(request.ResumeText, jobId);
        return Ok(result);
    }

    /// <summary>仅 ML 通道预测</summary>
    [HttpPost("ml/predict")]
    public IActionResult MLPredict([FromBody] MatchFeatures features)
    {
        var result = _ml.Predict(features);
        return Ok(result);
    }

    /// <summary>仅多智能体分析</summary>
    [HttpPost("multi-agent/analyze")]
    public async Task<IActionResult> MultiAgentAnalyze([FromBody] MultiAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ResumeText) || string.IsNullOrWhiteSpace(request.JobTitle))
            return BadRequest(new { error = "简历和岗位信息不能为空" });

        var result = await _multiAgent.AnalyzeAsync(
            request.ResumeText, request.JobTitle, request.JobRequirements ?? "");
        return Ok(result);
    }

    /// <summary>Graph RAG 岗位推荐</summary>
    [HttpPost("graph-rag/recommend")]
    public async Task<IActionResult> GraphRAGRecommend([FromBody] GraphRAGRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Skills))
            return BadRequest(new { error = "技能列表不能为空" });

        var result = await _graphRAG.RecommendJobsAsync(request.Skills, request.TopN > 0 ? request.TopN : 5);
        return Ok(result);
    }

    /// <summary>Graph RAG 学习路径</summary>
    [HttpPost("graph-rag/learning-path")]
    public async Task<IActionResult> GraphRAGLearningPath([FromBody] GraphRAGLearningPathRequest request)
    {
        var result = await _graphRAG.GenerateLearningPathAsync(request.Skills, request.TargetJob);
        return Ok(result);
    }

    /// <summary>重新训练 ML 模型</summary>
    [HttpPost("ml/retrain")]
    public IActionResult RetrainML()
    {
        _ml.TrainDefaultModel();
        return Ok(new { message = "ML 模型已重新训练" });
    }
}

// ═══ 请求 DTOs ═══
public class MatchV2Request
{
    public string ResumeText { get; set; } = "";
}

public class MultiAgentRequest
{
    public string ResumeText { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string? JobRequirements { get; set; }
}

public class GraphRAGRequest
{
    public string Skills { get; set; } = "";
    public int TopN { get; set; } = 5;
}

public class GraphRAGLearningPathRequest
{
    public string Skills { get; set; } = "";
    public string TargetJob { get; set; } = "";
}
