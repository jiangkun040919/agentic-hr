using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Models.DTOs;
using AIRecruitment.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly AppDbContext _db;

    public AuthController(IUserService userService, AppDbContext db)
    {
        _userService = userService;
        _db = db;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _userService.LoginAsync(request);
            return Ok(new { code = 200, message = "登录成功", data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _userService.RegisterAsync(request);
            return Ok(new { code = 200, message = "注册成功", data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    [HttpGet("info")]
    [Authorize]
    public async Task<IActionResult> GetUserInfo()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _userService.GetUserInfoAsync(userId);
        return Ok(new { code = 200, data = result });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        try
        {
            await _userService.ChangePasswordAsync(userId, request);
            return Ok(new { code = 200, message = "密码修改成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _userService.LogoutAsync(userId);
        return Ok(new { code = 200, message = "退出成功" });
    }

    [HttpPost("refresh")]
    [Authorize]
    public async Task<IActionResult> RefreshToken()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var newToken = await _userService.RefreshTokenAsync(userId);
        return Ok(new { code = 200, data = new { token = newToken } });
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        try
        {
            await _userService.UpdateProfileAsync(userId, request);
            return Ok(new { code = 200, message = "保存成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    /// <summary>
    /// 删除演示账号（仅限demo用户）
    /// </summary>
    [HttpDelete("users/{username}")]
    public async Task<IActionResult> DeleteUser(string username)
    {
        var user = await _db.SysUsers.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return Ok(new { code = 200, message = "用户不存在或已删除" });
        
        try
        {
            // Delete related records first (FK cascade not configured)
            var deliveries = await _db.Deliveries.Where(d => d.CandidateId == user.UserId).ToListAsync();
            _db.Deliveries.RemoveRange(deliveries);
            
            _db.SysUsers.Remove(user);
            await _db.SaveChangesAsync();
            return Ok(new { code = 200, message = $"已删除用户 {username}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = 500, message = $"删除失败: {ex.Message}" });
        }
    }

}
