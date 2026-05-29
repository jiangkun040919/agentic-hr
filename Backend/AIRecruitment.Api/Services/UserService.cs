using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Models.DTOs;

namespace AIRecruitment.Api.Services;

public interface IUserService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginResponse> RegisterAsync(RegisterRequest request);
    Task<UserInfoResponse> GetUserInfoAsync(int userId);
    Task ChangePasswordAsync(int userId, ChangePasswordRequest request);
    Task LogoutAsync(int userId);
    Task<string> RefreshTokenAsync(int userId);
    Task UpdateProfileAsync(int userId, UpdateProfileRequest request);
}

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public UserService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _context.SysUsers
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.Status == 1);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new Exception("用户名或密码错误");
        }

        // 生成Token
        var token = GenerateJwtToken(user);
        
        // 记录登录日志
        user.LastLogin = DateTime.Now;
        await _context.SaveChangesAsync();

        // 记录登录日志
        await LogLoginAsync(user.UserId, "success");

        return new LoginResponse(token, user.Role, user.UserId, user.Username);
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
    {
        // 检查用户名是否存在
        if (await _context.SysUsers.AnyAsync(u => u.Username == request.Username))
        {
            throw new Exception("用户名已存在");
        }

        // 创建用户
        var user = new SysUser
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            RealName = request.RealName,
            Phone = request.Phone,
            Email = request.Email,
            Status = 1,
            CreatedAt = DateTime.Now
        };

        _context.SysUsers.Add(user);
        
        // 如果是求职者，同时创建Candidate记录
        if (request.Role == "candidate")
        {
            var candidate = new Candidate
            {
                UserId = user.UserId,
                RealName = request.RealName,
                Phone = request.Phone ?? string.Empty,
                Email = request.Email,
                CreatedAt = DateTime.Now
            };
            _context.Candidates.Add(candidate);
        }

        await _context.SaveChangesAsync();
        
        // 生成Token
        var token = GenerateJwtToken(user);

        return new LoginResponse(token, user.Role, user.UserId, user.Username);
    }

    public async Task<UserInfoResponse> GetUserInfoAsync(int userId)
    {
        var user = await _context.SysUsers.FindAsync(userId);
        if (user == null) throw new Exception("用户不存在");

        var candidate = await _context.Candidates.FirstOrDefaultAsync(c => c.UserId == userId);
        return new UserInfoResponse(user.UserId, user.Username, user.Role,
            user.RealName, user.Phone, user.Email, null,
            candidate?.Education, candidate?.WorkYears,
            candidate?.ResumeContent, candidate?.ResumeUrl,
            candidate?.CandidateId);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _context.SysUsers.FindAsync(userId);
        if (user == null) throw new Exception("用户不存在");

        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
        {
            throw new Exception("原密码错误");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();
    }

    public async Task LogoutAsync(int userId)
    {
        await LogLoginAsync(userId, "logout");
    }

    public async Task<string> RefreshTokenAsync(int userId)
    {
        var user = await _context.SysUsers.FindAsync(userId);
        if (user == null) throw new Exception("用户不存在");
        return GenerateJwtToken(user);
    }

    public async Task UpdateProfileAsync(int userId, UpdateProfileRequest request)
    {
        var user = await _context.SysUsers.FindAsync(userId);
        if (user == null) throw new Exception("用户不存在");

        if (!string.IsNullOrWhiteSpace(request.RealName))
            user.RealName = request.RealName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Phone))
            user.Phone = request.Phone.Trim();
        if (!string.IsNullOrWhiteSpace(request.Email))
            user.Email = request.Email.Trim();

        // 同时更新 Candidate 表的简历字段
        var candidate = await _context.Candidates.FirstOrDefaultAsync(c => c.UserId == userId);
        if (candidate != null)
        {
            if (request.Education != null) candidate.Education = request.Education;
            if (request.WorkYears.HasValue) candidate.WorkYears = request.WorkYears;
            if (request.ResumeContent != null) candidate.ResumeContent = request.ResumeContent;
            if (request.ResumeUrl != null) candidate.ResumeUrl = request.ResumeUrl;
            if (!string.IsNullOrWhiteSpace(request.RealName)) candidate.RealName = request.RealName.Trim();
            if (!string.IsNullOrWhiteSpace(request.Phone)) candidate.Phone = request.Phone.Trim();
            if (!string.IsNullOrWhiteSpace(request.Email)) candidate.Email = request.Email?.Trim();
        }

        await _context.SaveChangesAsync();
    }

    private string GenerateJwtToken(SysUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "YourSuperSecretKey12345678901234567890"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "AIRecruitment",
            audience: _configuration["Jwt:Issuer"] ?? "AIRecruitment",
            claims: claims,
            expires: DateTime.Now.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task LogLoginAsync(int userId, string status)
    {
        // 简化实现，生产环境应获取真实IP和UserAgent
        _context.SysLoginLogs.Add(new SysLoginLog
        {
            UserId = userId,
            Status = status,
            CreatedAt = DateTime.Now
        });
        await _context.SaveChangesAsync();
    }
}