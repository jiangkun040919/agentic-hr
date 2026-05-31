using AIRecruitment.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/sys-config")]
public class SysConfigController : ControllerBase
{
    private readonly AppDbContext _context;

    public SysConfigController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取常用面试官配置（公开，无需登录）
    /// </summary>
    [HttpGet("common-interviewers")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCommonInterviewers()
    {
        var config = await _context.SysConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConfigKey == "CommonInterviewers");

        if (config == null || string.IsNullOrWhiteSpace(config.ConfigValue))
        {
            return Ok(new { code = 200, data = new List<object>() });
        }

        try
        {
            var ids = JsonSerializer.Deserialize<List<int>>(config.ConfigValue) ?? new List<int>();
            return Ok(new { code = 200, data = ids });
        }
        catch
        {
            return Ok(new { code = 200, data = new List<object>() });
        }
    }

    /// <summary>
    /// 保存常用面试官配置（需 HR 权限）
    /// </summary>
    [HttpPost("common-interviewers")]
    [Authorize]
    public async Task<IActionResult> SaveCommonInterviewers([FromBody] CommonInterviewersRequest request)
    {
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (userRole != "hr" && userRole != "admin")
        {
            return Forbid();
        }

        var config = await _context.SysConfigs
            .FirstOrDefaultAsync(c => c.ConfigKey == "CommonInterviewers");

        var jsonValue = JsonSerializer.Serialize(request.InterviewerIds ?? new List<int>());

        if (config == null)
        {
            config = new SysConfig
            {
                ConfigKey = "CommonInterviewers",
                ConfigValue = jsonValue,
                Description = "常用面试官配置（用于安排面试时的快捷选择）"
            };
            _context.SysConfigs.Add(config);
        }
        else
        {
            config.ConfigValue = jsonValue;
        }

        await _context.SaveChangesAsync();
        return Ok(new { code = 200, message = "保存成功" });
    }
}

public class CommonInterviewersRequest
{
    public List<int> InterviewerIds { get; set; } = new List<int>();
}
