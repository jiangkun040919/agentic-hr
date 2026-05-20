using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;

namespace AIRecruitment.Api.Services;

public class ScheduledTasksService
{
    private readonly AppDbContext _context;
    private readonly KnowledgeGraphService? _graph;
    private readonly ILogger<ScheduledTasksService> _logger;

    public ScheduledTasksService(AppDbContext context, KnowledgeGraphService? graph = null, ILogger<ScheduledTasksService>? logger = null)
    {
        _context = context;
        _graph = graph;
        _logger = logger!;
    }

    /// <summary>自动下架过期岗位</summary>
    public async Task AutoCloseExpiredJobs()
    {
        var expiredJobs = await _context.Jobs
            .Where(j => j.Status == 1 && j.ExpiredAt != null && j.ExpiredAt < DateTime.Now)
            .ToListAsync();

        foreach (var job in expiredJobs)
        {
            job.Status = 0;
            _logger.LogInformation("[定时任务] 自动下架过期岗位: {title} (ID={id})", job.Title, job.JobId);
        }

        if (expiredJobs.Count > 0)
            await _context.SaveChangesAsync();
    }

    /// <summary>提醒 HR 处理超过 3 天未查看的候选人</summary>
    public async Task RemindStaleCandidates()
    {
        var threeDaysAgo = DateTime.Now.AddDays(-3);
        var staleDeliveries = await _context.Deliveries
            .Include(d => d.Job)
            .Include(d => d.Candidate)
            .Where(d => d.Status == 0 && d.DeliverTime < threeDaysAgo)
            .GroupBy(d => d.HrId)
            .ToListAsync();

        foreach (var group in staleDeliveries)
        {
            var count = group.Count();
            // 为每个 HR 创建一条通知
            var notification = new Notification
            {
                UserId = group.Key,
                Type = "delivery",
                Title = "待处理候选人提醒",
                Content = $"您有 {count} 位候选人超过 3 天未处理，请及时查看简历",
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);
            _logger.LogInformation("[定时任务] HR(ID={hrId}) 有 {count} 位候选人待处理", group.Key, count);
        }

        if (staleDeliveries.Count > 0)
            await _context.SaveChangesAsync();
    }

    /// <summary>生成一周招聘数据摘要</summary>
    public async Task GenerateWeeklyReport()
    {
        var weekAgo = DateTime.Now.AddDays(-7);
        var hrIds = await _context.SysUsers
            .Where(u => u.Role == "hr")
            .Select(u => u.UserId)
            .ToListAsync();

        foreach (var hrId in hrIds)
        {
            var deliveries = await _context.Deliveries
                .Where(d => d.HrId == hrId && d.DeliverTime >= weekAgo)
                .ToListAsync();

            var interviewed = deliveries.Count(d => d.Status >= 2);
            var hired = deliveries.Count(d => d.Status >= 3);

            var notification = new Notification
            {
                UserId = hrId,
                Type = "system",
                Title = "本周招聘周报",
                Content = $"过去一周共收到 {deliveries.Count} 份简历，" +
                          $"其中 {interviewed} 人进入面试，" +
                          $"入职 {hired} 人。" +
                          (deliveries.Count == 0 ? "本周暂无新投递。" : "请登录系统查看详情。"),
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("[定时任务] 周报已生成，覆盖 {count} 位HR", hrIds.Count);
    }

    /// <summary>每周保存一次知识图谱快照用于时态演化分析</summary>
    public async Task TakeGraphSnapshotAsync()
    {
        if (_graph == null) return;
        try
        {
            var period = DateTime.Now.ToString("yyyy-MM");
            var snapshots = await _graph.TakeSnapshotAsync(_context, period);
            _logger.LogInformation("[定时任务] 图谱快照已保存: {count}个岗位, period={period}", snapshots.Count, period);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[定时任务] 图谱快照失败: {msg}", ex.Message);
        }
    }
}
