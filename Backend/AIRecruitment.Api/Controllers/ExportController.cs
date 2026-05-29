using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly ExportService _export;

    public ExportController(ExportService export)
    {
        _export = export;
    }

    /// <summary>导出投递记录 Excel</summary>
    [HttpGet("deliveries")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> ExportDeliveries()
    {
        try
        {
            var bytes = await _export.ExportDeliveriesAsync();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"投递记录_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = ex.Message });
        }
    }

    /// <summary>导出候选人数据 Excel</summary>
    [HttpGet("candidates")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> ExportCandidates()
    {
        try
        {
            var bytes = await _export.ExportCandidatesAsync();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"候选人数据_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = ex.Message });
        }
    }

    /// <summary>导出准确率评测报告 Excel</summary>
    [HttpGet("benchmark")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> ExportBenchmark()
    {
        try
        {
            var bytes = await _export.ExportBenchmarkReportAsync();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"准确率评测报告_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = ex.Message });
        }
    }
}
