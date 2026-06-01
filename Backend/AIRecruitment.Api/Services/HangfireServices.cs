using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;

namespace AIRecruitment.Api.Services;

public interface IDailyStatisticsService
{
    Task GenerateDailyStatistics();
}

public class DailyStatisticsService : IDailyStatisticsService
{
    private readonly AppDbContext _context;

    public DailyStatisticsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task GenerateDailyStatistics()
    {
        var today = DateTime.UtcNow.Date;
        var hrIds = await _context.SysUsers
            .Where(u => u.Role == "hr")
            .Select(u => u.UserId)
            .ToListAsync();

        foreach (var hrId in hrIds)
        {
            // 统计今日数据
            var deliveries = await _context.Deliveries
                .Where(d => d.HrId == hrId && d.DeliverTime.Date == today)
                .CountAsync();

            var interviews = await _context.Interviews
                .Where(i => i.InterviewerId == hrId && i.ScheduleTime.Date == today)
                .CountAsync();

            // 记录日志
            Console.WriteLine($"[DailyStats] HR:{hrId} - 投递:{deliveries}, 面试:{interviews}");
        }
    }
}

public interface IJobExpirationService
{
    Task CheckAndExpireJobs();
}

public class JobExpirationService : IJobExpirationService
{
    private readonly AppDbContext _context;

    public JobExpirationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task CheckAndExpireJobs()
    {
        var expiredJobs = await _context.Jobs
            .Where(j => j.Status == 1 && j.ExpiredAt != null && j.ExpiredAt < DateTime.UtcNow)
            .ToListAsync();

        foreach (var job in expiredJobs)
        {
            job.Status = 2; // 已过期
            job.UpdatedAt = DateTime.UtcNow;
            Console.WriteLine($"[JobExpiration] 岗位已过期: {job.JobId} - {job.Title}");
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"[JobExpiration] 已处理 {expiredJobs.Count} 个过期岗位");
    }
}

public interface IResumeCleanupService
{
    Task CleanupOldResumes();
}

public class ResumeCleanupService : IResumeCleanupService
{
    private readonly AppDbContext _context;

    public ResumeCleanupService(AppDbContext context)
    {
        _context = context;
    }

    public async Task CleanupOldResumes()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-180);
        
        // 清理旧的AI分析记录（保留180天）
        var oldAnalyses = await _context.AIResumeAnalyses
            .Where(a => a.CreatedAt < cutoffDate)
            .ToListAsync();

        _context.AIResumeAnalyses.RemoveRange(oldAnalyses);
        
        // 清理旧的投递记录（逻辑删除）
        var oldDeliveries = await _context.Deliveries
            .Where(d => d.DeliverTime < cutoffDate && d.Status == 4) // 只清理已淘汰的
            .ToListAsync();

        foreach (var delivery in oldDeliveries)
        {
            delivery.Status = -1; // 标记为已删除
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"[ResumeCleanup] 已清理 {oldAnalyses.Count} 条AI分析记录");
    }
}