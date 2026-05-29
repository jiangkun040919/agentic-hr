using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;
using Hangfire;

namespace AIRecruitment.Api.Controllers;

/// <summary>
/// 实时岗位采集控制 — 手动触发 + 查看统计
/// </summary>
[ApiController]
[Route("api/collector")]
[Authorize]
public class JobCollectorController : ControllerBase
{
    /// <summary>立即执行一次岗位采集（手动触发）</summary>
    [HttpPost("run")]
    public async Task<IActionResult> RunNow([FromServices] RealtimeJobCollectorService collector)
    {
        var result = await collector.CollectAsync();
        return Ok(new
        {
            message = $"采集完成：新增 {result.Inserted} 个岗位",
            detail = new
            {
                platformJobs = result.PlatformCount,
                aiGenerated = result.AICount,
                result.Inserted,
                duration = (result.CompletedAt - result.StartedAt).TotalSeconds.ToString("F1") + "s"
            }
        });
    }

    /// <summary>立即触发采集（通过Hangfire后台执行，不等待）</summary>
    [HttpPost("trigger")]
    public IActionResult TriggerBackground()
    {
        BackgroundJob.Enqueue<RealtimeJobCollectorService>(x => x.CollectAsync());
        return Ok(new { message = "已加入采集队列，稍后刷新页面查看新岗位" });
    }

    /// <summary>查看定时任务状态</summary>
    [HttpGet("status")]
    public IActionResult Status([FromServices] IRecurringJobManager manager)
    {
        return Ok(new
        {
            schedule = "每2小时自动采集（Cron: 0 */2 * * *）",
            dataSources = new[] { "招聘平台", "企业官网", "行业报告", "AI趋势分析" },
            hangfireDashboard = "/hangfire"
        });
    }
}
