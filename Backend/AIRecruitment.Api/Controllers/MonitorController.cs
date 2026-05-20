using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MonitorController : ControllerBase
{
    private readonly IHealthMonitorService _healthMonitor;
    private readonly IProcessMonitorService _processMonitor;

    public MonitorController(IHealthMonitorService healthMonitor, IProcessMonitorService processMonitor)
    {
        _healthMonitor = healthMonitor;
        _processMonitor = processMonitor;
    }

    [HttpGet("health")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            var result = await _healthMonitor.GetLatestResultsAsync();
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return BadRequest(new { code = 400, message = ex.Message }); }
    }

    [HttpPost("process/run")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> RunProcess([FromBody] RunProcessRequest request)
    {
        try
        {
            var result = await _processMonitor.RunAsync(request.ProcessPath, request.Arguments ?? "", request.TimeoutMs);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return BadRequest(new { code = 400, message = ex.Message }); }
    }
}

public record RunProcessRequest(string ProcessPath, string? Arguments = null, int TimeoutMs = 30000);
