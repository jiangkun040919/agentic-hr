using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/data-collection")]
public class DataCollectionController : ControllerBase
{
    private readonly DataCollectionService _collector;
    private readonly IAIService _ai;

    public DataCollectionController(DataCollectionService collector, IAIService ai)
    {
        _collector = collector;
        _ai = ai;
    }

    [HttpPost("run-etl")]
    public async Task<IActionResult> RunETL()
    {
        var report = await _collector.RunETLPipelineAsync();
        return Ok(new { code = 200, data = report });
    }

    [HttpGet("test-dataset")]
    public IActionResult GetTestDataset()
    {
        var dataset = _collector.GenerateTestDataset();
        return Ok(new { code = 200, data = new { total = dataset.Count, items = dataset } });
    }

    /// <summary>自动准确率评测：对测试数据集批量运行 JD 生成并评分</summary>
    [HttpPost("evaluate-accuracy")]
    public async Task<IActionResult> EvaluateAccuracy([FromQuery] int sampleSize = 10)
    {
        var dataset = _collector.GenerateTestDataset().Take(sampleSize).ToList();
        var results = new List<object>();
        var successCount = 0;

        foreach (var item in dataset)
        {
            try
            {
                var jd = await _ai.GenerateJDAsync(item.Requirements);
                var hasTitle = !string.IsNullOrEmpty(jd.Title) && jd.Title != "待定岗位";
                var hasReqs = jd.Versions.Count > 0 && jd.Versions[0].Requirements.Count > 0;
                var hasJD = jd.Versions.Count > 0 && jd.Versions[0].Responsibilities.Count > 0;
                var passed = hasTitle && hasReqs && hasJD;
                if (passed) successCount++;
                results.Add(new { input = item.Title, outputTitle = jd.Title, passed, hasTitle, hasReqs, hasJD, versions = jd.Versions.Count });
            }
            catch { results.Add(new { input = item.Title, passed = false, error = "AI调用失败" }); }
        }

        var accuracy = (double)successCount / sampleSize * 100;
        return Ok(new { code = 200, data = new { sampleSize, successCount, accuracy, results } });
    }
}
