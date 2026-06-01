using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Models.DTOs;
using AIRecruitment.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;

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
            return BadRequest(new { code = 400, message = "服务器内部错误" });
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
            return BadRequest(new { code = 400, message = "服务器内部错误" });
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
            return BadRequest(new { code = 400, message = "服务器内部错误" });
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
            return BadRequest(new { code = 400, message = "服务器内部错误" });
        }
    }

    /// <summary>上传简历文件（通用 — 不绑定投递记录）</summary>
    [HttpPost("upload-resume")]
    [Authorize]
    public async Task<IActionResult> UploadResume([FromBody] UploadResumeRequest request)
    {
        try
        {
            var fileName = request.FileName ?? "resume.pdf";
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext != ".pdf" && ext != ".docx" && ext != ".doc")
                return Ok(new { code = 400, message = "仅支持 PDF (.pdf) 和 Word (.docx/.doc) 格式" });

            if (request.FileBase64.Length > 20 * 1024 * 1024)
                return Ok(new { code = 400, message = "文件大小不能超过 15MB" });

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "resumes");
            Directory.CreateDirectory(uploadsDir);

            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var safeName = $"{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{fileName}";
            var filePath = Path.Combine(uploadsDir, safeName);

            var fileBytes = Convert.FromBase64String(request.FileBase64);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            // 提取文本内容，同步到在线简历
            string? extractedText = null;
            try
            {
                var pdfService = HttpContext.RequestServices.GetRequiredService<IPdfExtractService>();
                var (text, _) = await pdfService.ExtractBase64Async(request.FileBase64, fileName, userId);
                extractedText = !string.IsNullOrWhiteSpace(text) ? text : null;
            }
            catch { /* 提取失败不影响上传 */ }

            var url = $"/uploads/resumes/{safeName}";
            return Ok(new { code = 200, message = "上传成功", data = new { url, resumeContent = extractedText } });
        }
        catch (FormatException)
        {
            return Ok(new { code = 400, message = "文件数据格式错误" });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = "服务器内部错误" });
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
            return StatusCode(500, new { code = 500, message = "服务器内部错误" });
        }
    }

}
