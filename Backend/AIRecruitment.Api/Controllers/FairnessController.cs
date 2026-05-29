using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,hr")]
public class FairnessController : ControllerBase
{
    private readonly FairnessAuditService _audit;

    public FairnessController(FairnessAuditService audit)
    {
        _audit = audit;
    }

    /// <summary>执行公平性审计</summary>
    [HttpPost("audit")]
    public async Task<IActionResult> RunAudit()
    {
        try
        {
            var report = await _audit.RunAuditAsync();
            return Ok(new { code = 200, data = report });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = ex.Message });
        }
    }
}
