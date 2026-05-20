using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/test")]
[Authorize(Roles = "hr,admin")]
public class TestController : ControllerBase
{
    private readonly IAccuracyTestService _testService;

    public TestController(IAccuracyTestService testService) { _testService = testService; }

    /// <summary>运行全部评测（≥100条）并返回报告</summary>
    [HttpPost("run-all")]
    public async Task<IActionResult> RunAllTests([FromQuery] int count = 100)
    {
        try
        {
            var report = await _testService.RunAllTestsAsync(Math.Max(count, 100));
            return Ok(new { code = 200, data = report });
        }
        catch (Exception ex) { return BadRequest(new { code = 500, message = ex.Message }); }
    }

    /// <summary>仅测试 JD 解析准确率</summary>
    [HttpPost("jd-parse")]
    public async Task<IActionResult> TestJDParse([FromQuery] int count = 100)
    {
        try
        {
            var set = await _testService.GenerateJDTestSetAsync(count);
            var metric = await _testService.TestJDParseAsync(set);
            return Ok(new { code = 200, data = new { accuracy = metric.Accuracy, total = metric.TotalFields, correct = metric.CorrectFields, errors = metric.Errors } });
        }
        catch (Exception ex) { return BadRequest(new { code = 500, message = ex.Message }); }
    }

    /// <summary>仅测试简历提取准确率</summary>
    [HttpPost("resume-extract")]
    public async Task<IActionResult> TestResumeExtract([FromQuery] int count = 100)
    {
        try
        {
            var set = await _testService.GenerateResumeTestSetAsync(count);
            var metric = await _testService.TestResumeExtractionAsync(set);
            return Ok(new { code = 200, data = new { accuracy = metric.Accuracy, total = metric.TotalFields, correct = metric.CorrectFields, errors = metric.Errors } });
        }
        catch (Exception ex) { return BadRequest(new { code = 500, message = ex.Message }); }
    }

    /// <summary>仅测试人岗匹配准确率</summary>
    [HttpPost("matching")]
    public async Task<IActionResult> TestMatching([FromQuery] int count = 100)
    {
        try
        {
            var set = await _testService.GenerateMatchTestSetAsync(count);
            var metric = await _testService.TestMatchingAsync(set);
            return Ok(new { code = 200, data = new { accuracy = metric.Accuracy, total = metric.TotalFields, correct = metric.CorrectFields, errors = metric.Errors } });
        }
        catch (Exception ex) { return BadRequest(new { code = 500, message = ex.Message }); }
    }
}
