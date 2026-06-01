using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Models.DTOs;
using AIRecruitment.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobController : ControllerBase
{
    private readonly IJobService _jobService;
    private readonly IAIService _aiService;
    private readonly KnowledgeGraphService? _graph;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;

    public JobController(IJobService jobService, IAIService aiService, IConfiguration configuration, KnowledgeGraphService? graph = null, AppDbContext db = null!)
    {
        _jobService = jobService;
        _aiService = aiService;
        _configuration = configuration;
        _graph = graph;
        _db = db;
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetJobList([FromQuery] JobListParams p)
    {
        var result = await _jobService.GetJobListAsync(p);
        return Ok(new { code = 200, data = new { items = result.Items, total = result.Total } });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetJobDetail(int id)
    {
        var result = await _jobService.GetJobDetailAsync(id);
        return Ok(new { code = 200, data = result });
    }

    [HttpGet("my")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> GetMyJobs([FromQuery] JobListParams p)
    {
        var hrId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _jobService.GetMyJobsAsync(hrId, p);
        return Ok(new { code = 200, data = new { items = result.Items, total = result.Total } });
    }

    [HttpPost]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> CreateJob([FromBody] JobFormData data)
    {
        var hrId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _jobService.CreateJobAsync(hrId, data);
        return Ok(new { code = 200, message = "创建成功", data = result });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> UpdateJob(int id, [FromBody] JobFormData data)
    {
        try
        {
            var hrId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var isAdmin = User.IsInRole("admin");
            var result = await _jobService.UpdateJobAsync(id, hrId, data, isAdmin);
            return Ok(new { code = 200, message = "更新成功", data = result });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateJob Error] id={id}, error={ex.Message}, stack={ex.StackTrace}");
            return StatusCode(500, new { code = 500, message = "服务器内部错误" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> DeleteJob(int id)
    {
        await _jobService.DeleteJobAsync(id);
        return Ok(new { code = 200, message = "删除成功" });
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> UpdateJobStatus(int id, [FromBody] StatusUpdateRequest request)
    {
        await _jobService.UpdateJobStatusAsync(id, request.Status);
        return Ok(new { code = 200, message = "状态更新成功" });
    }

    /// <summary>AI 智能生成 JD</summary>
    [HttpPost("generate-jd")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> GenerateJD([FromBody] GenerateJDRequest request)
    {
        try
        {
            var result = await _aiService.GenerateJDAsync(request.Brief);
            // 反幻觉校验：验证 AI 生成的技能是否在图谱中有支撑
            object? verification = null;
            if (_graph != null)
            {
                try
                {
                    var skills = result.Versions
                        .SelectMany(v => v.Requirements)
                        .SelectMany(r => r.Split(new[] { '，', ',', '、', '\n', '：', ':' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 1)
                        .ToList();
                    verification = await _graph.VerifySkillsAsync(skills);
                }
                catch { /* 图谱不可用 */ }
            }
            return Ok(new { code = 200, data = result, verification });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = "服务器内部错误" });
        }
    }

    /// <summary>批量导入岗位（爬虫推送入口，支持 X-Api-Key 认证）</summary>
    [HttpPost("batch-import")]
    [AllowAnonymous]
    public async Task<IActionResult> BatchImport([FromBody] List<JobImportItem> items)
    {
        // 检查 API Key 或 JWT
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        var expectedKey = _configuration["Crawler:ApiKey"];
        var isApiKeyValid = !string.IsNullOrEmpty(apiKey) && apiKey == expectedKey;
        var isJwtValid = User.Identity?.IsAuthenticated == true && (User.IsInRole("hr") || User.IsInRole("admin"));

        if (!isApiKeyValid && !isJwtValid)
            return Unauthorized(new { code = 401, message = "需要有效的认证信息" });

        try
        {
            var count = await _jobService.BatchImportAsync(items);
            return Ok(new { code = 200, message = $"成功导入 {count} 条，跳过 {items.Count - count} 条重复", data = new { imported = count } });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = "服务器内部错误" }); }
    }

    [HttpGet("dept-stats")]
    public async Task<IActionResult> DeptStats()
    {
        var stats = await _db.Jobs
            .Where(j => j.Status == 1)
            .GroupBy(j => j.Dept)
            .Select(g => new { dept = g.Key, count = g.Count() })
            .ToListAsync();
        
        return Ok(new { code = 200, data = stats });
    }
}

public record GenerateJDRequest([Required] string Brief);