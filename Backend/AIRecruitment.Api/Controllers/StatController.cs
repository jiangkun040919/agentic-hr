using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatController : ControllerBase
{
    private readonly IStatisticsService _statisticsService;

    public StatController(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    private int GetHrId()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr != null && int.TryParse(userIdStr, out var uid)) return uid;
        return 0;
    }

    // 获取过滤用的 HrId：null = 看全部数据
    private int? GetFilterHrId()
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        // 统一不过滤 HrId，让所有 HR/Admin 都能看到全部数据
        // 如需按 HR 隔离，取消下面注释：
        // return role == "admin" ? null : GetHrId();
        return null;
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = "admin,hr")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            var result = await _statisticsService.GetDashboardDataAsync(GetFilterHrId());
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    [HttpGet("funnel")]
    [Authorize(Roles = "admin,hr")]
    public async Task<IActionResult> GetFunnel(
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int? jobId)
    {
        try
        {
            var result = await _statisticsService.GetFunnelDataAsync((GetFilterHrId() ?? 0), startDate, endDate);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    [HttpGet("job")]
    [Authorize(Roles = "admin,hr")]
    public async Task<IActionResult> GetJobStats(
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        try
        {
            var result = await _statisticsService.GetJobStatsAsync((GetFilterHrId() ?? 0), startDate, endDate);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    [HttpGet("source")]
    [Authorize(Roles = "admin,hr")]
    public async Task<IActionResult> GetSourceStats(
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        try
        {
            var result = await _statisticsService.GetResumeSourceStatsAsync(startDate, endDate);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    [HttpGet("trend")]
    [Authorize(Roles = "admin,hr")]
    public async Task<IActionResult> GetTrend([FromQuery] int days = 30, [FromQuery] string? type = null)
    {
        try
        {
            var result = await _statisticsService.GetTrendDataAsync(days, type);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    [HttpGet("flow-pool")]
    [Authorize(Roles = "admin,hr")]
    public async Task<IActionResult> GetFlowPool()
    {
        try
        {
            var result = await _statisticsService.GetFlowPoolDataAsync((GetFilterHrId() ?? 0));
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    [HttpGet("multi-trend")]
    [Authorize(Roles = "admin,hr")]
    public async Task<IActionResult> GetMultiTrend([FromQuery] string? dimension = "week")
    {
        try
        {
            var result = await _statisticsService.GetMultiTrendDataAsync(dimension ?? "week");
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    [HttpGet("hire-rate")]
    [Authorize(Roles = "admin,hr")]
    public async Task<IActionResult> GetHireRate([FromQuery] string? dimension = "week")
    {
        try
        {
            var result = await _statisticsService.GetHireRateDataAsync((GetFilterHrId() ?? 0), dimension ?? "week");
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }

    [HttpGet("hot-jobs")]
    [Authorize(Roles = "admin,hr")]
    public async Task<IActionResult> GetHotJobs([FromQuery] int? limit)
    {
        try
        {
            var result = await _statisticsService.GetHotJobDetailsAsync((GetFilterHrId() ?? 0));
            if (limit.HasValue && limit.Value > 0)
                result = result.Take(limit.Value).ToList();
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return Ok(new { code = 500, message = ex.Message }); }
    }
}
