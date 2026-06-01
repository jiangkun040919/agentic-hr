using AIRecruitment.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly AppDbContext _context;

    public UserController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("interviewers")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInterviewers()
    {
        var interviewers = await _context.SysUsers
            .Where(u => u.Role == "hr")
            .Select(u => new
            {
                userId = u.UserId,
                realName = u.RealName ?? u.Username,
                roleName = u.Role,
                phone = u.Phone,
                email = u.Email
            })
            .ToListAsync();

        return Ok(new { code = 200, data = interviewers });
    }

    [HttpPost("interviewer")]
    [Authorize]
    public async Task<IActionResult> CreateInterviewer([FromBody] CreateInterviewerRequest request)
    {
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (userRole != "hr" && userRole != "admin")
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { code = 400, message = "用户名不能为空" });
        }

        var exists = await _context.SysUsers.AnyAsync(u => u.Username == request.Username);
        if (exists)
        {
            return BadRequest(new { code = 400, message = "用户名已存在" });
        }

        var user = new SysUser
        {
            Username = request.Username.Trim(),
            RealName = request.RealName?.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password ?? "123456"),
            Role = "hr",
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.SysUsers.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { code = 200, message = "创建成功", data = new { userId = user.UserId } });
    }

    [HttpPut("interviewer/{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateInterviewer(int id, [FromBody] UpdateInterviewerRequest request)
    {
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (userRole != "hr" && userRole != "admin")
        {
            return Forbid();
        }

        var user = await _context.SysUsers.FindAsync(id);
        if (user == null || user.Role != "hr")
        {
            return NotFound(new { code = 404, message = "面试官不存在" });
        }

        if (!string.IsNullOrWhiteSpace(request.RealName))
            user.RealName = request.RealName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Phone))
            user.Phone = request.Phone.Trim();
        if (!string.IsNullOrWhiteSpace(request.Email))
            user.Email = request.Email.Trim();
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        await _context.SaveChangesAsync();
        return Ok(new { code = 200, message = "更新成功" });
    }

    [HttpDelete("interviewer/{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteInterviewer(int id)
    {
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (userRole != "hr" && userRole != "admin")
        {
            return Forbid();
        }

        var user = await _context.SysUsers.FindAsync(id);
        if (user == null || user.Role != "hr")
        {
            return NotFound(new { code = 404, message = "面试官不存在" });
        }

        _context.SysUsers.Remove(user);
        await _context.SaveChangesAsync();
        return Ok(new { code = 200, message = "删除成功" });
    }
}

public class CreateInterviewerRequest
{
    public string Username { get; set; } = string.Empty;
    public string? RealName { get; set; }
    public string? Password { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class UpdateInterviewerRequest
{
    public string? RealName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}