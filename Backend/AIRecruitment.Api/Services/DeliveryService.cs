using Hangfire;
using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Models.DTOs;

namespace AIRecruitment.Api.Services;

public interface IDeliveryService
{
    Task<PagedResponse<DeliveryResponse>> GetDeliveryListAsync(DeliveryListParams p);
    Task<DeliveryResponse> GetDeliveryDetailAsync(int id);
    Task<DeliveryResponse> SubmitDeliveryAsync(DeliveryFormData data, int userId);
    Task UpdateDeliveryStatusAsync(int id, int status, string? remark);
    Task<List<DeliveryResponse>> GetMyDeliveriesAsync(int userId);
    Task CancelDeliveryAsync(int id);
    Task<DeliveryResponse> UpdateDeliveryInfoAsync(int id, DeliveryFormData data);
    Task SetAIInterviewPermissionAsync(int id, bool allow, DateTime? deadline = null);
    Task SaveResumeTextAsync(int id, string resumeText);
    Task SaveResumeFilePathAsync(int id, string filePath);
    Task StartInternshipAsync(int id, StartInternshipRequest req, int operatedBy);
    Task FormalHireAsync(int id, FormalHireRequest req, int operatedBy);
}

public class DeliveryService : IDeliveryService
{
    private readonly AppDbContext _context;
    private readonly IRabbitMQService? _rabbitMQ;
    private readonly IServiceProvider _serviceProvider;

    public DeliveryService(AppDbContext context, IRabbitMQService? rabbitMQ = null, IServiceProvider? serviceProvider = null)
    {
        _context = context;
        _rabbitMQ = rabbitMQ;
        _serviceProvider = serviceProvider!;
    }

    /// <summary>从投递快照中获取联系方式，若快照为空则回退到候选人基础信息</summary>
    private static (string name, string phone, string? email, string? edu, int? years, string? resumeUrl) GetContactInfo(Delivery d)
    {
        bool hasSnapshot = !string.IsNullOrEmpty(d.ContactPhone);
        return (
            hasSnapshot ? d.ContactName : (d.Candidate?.RealName ?? d.ContactName),
            hasSnapshot ? d.ContactPhone : (d.Candidate?.Phone ?? d.ContactPhone),
            hasSnapshot ? d.ContactEmail : (d.Candidate?.Email ?? d.ContactEmail),
            hasSnapshot ? d.ContactEducation : (d.Candidate?.Education ?? d.ContactEducation),
            hasSnapshot ? d.ContactWorkYears : (d.Candidate?.WorkYears ?? d.ContactWorkYears),
            hasSnapshot ? d.ContactResumeUrl : (d.Candidate?.ResumeUrl ?? d.ContactResumeUrl)
        );
    }

    public async Task<PagedResponse<DeliveryResponse>> GetDeliveryListAsync(DeliveryListParams p)
    {
        var query = _context.Deliveries
            .Include(d => d.Job)
            .Include(d => d.Candidate)
            .AsQueryable();

        if (p.HrId.HasValue)
            query = query.Where(d => d.HrId == p.HrId);
        if (p.JobId.HasValue)
            query = query.Where(d => d.JobId == p.JobId);
        if (p.Status.HasValue)
            query = query.Where(d => d.Status == p.Status);
        if (!string.IsNullOrEmpty(p.Keyword))
            query = query.Where(d =>
                d.ContactName.Contains(p.Keyword) || d.ContactPhone.Contains(p.Keyword) ||
                (d.Candidate != null && (d.Candidate.RealName.Contains(p.Keyword) || d.Candidate.Phone.Contains(p.Keyword))));

        var total = await query.CountAsync();
        var deliveries = await query
            .OrderBy(d => d.DeliverTime)
            .Skip((p.Page - 1) * p.PageSize)
            .Take(p.PageSize)
            .ToListAsync();

        var items = deliveries.Select(d => {
            var c = GetContactInfo(d);
            return new DeliveryResponse(
                d.DeliveryId, d.JobId, d.Job!.Title, d.CandidateId, c.name,
                c.phone, c.email, c.edu, c.years,
                c.resumeUrl, d.Status, d.HrId, d.DeliverTime, d.UpdateTime, d.Remark,
                d.AllowAIInterview, d.AIInterviewDeadline);
        }).ToList();

        return new PagedResponse<DeliveryResponse>(items, total, p.Page, p.PageSize);
    }

    public async Task<DeliveryResponse> GetDeliveryDetailAsync(int id)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Job)
            .Include(d => d.Candidate)
            .Include(d => d.AIScore)
            .FirstOrDefaultAsync(d => d.DeliveryId == id);

        if (delivery == null) throw new Exception("投递记录不存在");

        var c = GetContactInfo(delivery);
        return new DeliveryResponse(
            delivery.DeliveryId, delivery.JobId, delivery.Job!.Title, delivery.CandidateId,
            c.name, c.phone, c.email,
            c.edu, c.years, c.resumeUrl,
            delivery.Status, delivery.HrId, delivery.DeliverTime, delivery.UpdateTime, delivery.Remark,
            delivery.AllowAIInterview, delivery.AIInterviewDeadline,
            delivery.ResumeText);
    }

    public async Task<DeliveryResponse> SubmitDeliveryAsync(DeliveryFormData data, int userId)
    {
        var job = await _context.Jobs.FindAsync(data.JobId);
        if (job == null || job.Status != 1) throw new Exception("岗位不存在或已关闭");

        var candidate = await _context.Candidates.FirstOrDefaultAsync(c => c.UserId == userId);
        if (candidate == null)
        {
            candidate = new Candidate
            {
                UserId = userId,
                RealName = data.CandidateName,
                Phone = data.Phone,
                Email = data.Email,
                Education = data.Education,
                WorkYears = data.WorkYears,
                ResumeUrl = data.ResumeUrl,
                CreatedAt = DateTime.Now
            };
            _context.Candidates.Add(candidate);
            await _context.SaveChangesAsync();
        }

        // 每次投递都在 Delivery 上保存独立的联系方式快照
        var delivery = new Delivery
        {
            JobId = data.JobId,
            CandidateId = candidate.CandidateId,
            HrId = job.HrId,
            Status = 0,
            DeliverTime = DateTime.Now,
            ContactName = data.CandidateName,
            ContactPhone = data.Phone,
            ContactEmail = data.Email,
            ContactEducation = data.Education,
            ContactWorkYears = data.WorkYears,
            ContactResumeUrl = data.ResumeUrl
        };

        _context.Deliveries.Add(delivery);
        await _context.SaveChangesAsync();

        if (_rabbitMQ != null)
            await _rabbitMQ.PublishAsync("ai_resume_analyze", new { delivery.DeliveryId });

        // Hangfire 兜底：RabbitMQ 不可用时仍能自动触发 AI 分析
        Hangfire.BackgroundJob.Enqueue<IAIService>(s => s.AnalyzeResumeAsync(delivery.DeliveryId));
        Hangfire.BackgroundJob.Enqueue<IAIService>(s => s.ScoreResumeAsync(delivery.DeliveryId));

        return new DeliveryResponse(
            delivery.DeliveryId, delivery.JobId, job.Title, candidate.CandidateId,
            delivery.ContactName, delivery.ContactPhone, delivery.ContactEmail,
            delivery.ContactEducation, delivery.ContactWorkYears, delivery.ContactResumeUrl,
            delivery.Status, delivery.HrId,
            delivery.DeliverTime, delivery.UpdateTime, delivery.Remark,
            delivery.AllowAIInterview, delivery.AIInterviewDeadline);
    }

    public async Task UpdateDeliveryStatusAsync(int id, int status, string? remark)
    {
        var delivery = await _context.Deliveries.FindAsync(id);
        if (delivery == null) throw new Exception("投递记录不存在");

        delivery.Status = status;
        delivery.UpdateTime = DateTime.Now;
        delivery.Remark = remark;
        await _context.SaveChangesAsync();
    }

    public async Task<List<DeliveryResponse>> GetMyDeliveriesAsync(int userId)
    {
        var candidate = await _context.Candidates.FirstOrDefaultAsync(c => c.UserId == userId);
        if (candidate == null) return new List<DeliveryResponse>();

        var deliveries = await _context.Deliveries
            .Include(d => d.Job)
            .Where(d => d.CandidateId == candidate.CandidateId)
            .OrderByDescending(d => d.DeliverTime)
            .ToListAsync();
        
        return deliveries.Select(d => {
            var c = GetContactInfo(d);
            return new DeliveryResponse(
                d.DeliveryId, d.JobId, d.Job!.Title, d.CandidateId, c.name,
                c.phone, c.email, c.edu, c.years,
                c.resumeUrl, d.Status, d.HrId, d.DeliverTime, d.UpdateTime, d.Remark,
                d.AllowAIInterview, d.AIInterviewDeadline);
        }).ToList();
    }

    public async Task CancelDeliveryAsync(int id)
    {
        var delivery = await _context.Deliveries.FindAsync(id);
        if (delivery == null) throw new Exception("投递记录不存在");

        // 先删除所有相关记录
        var interviews = await _context.Interviews.Where(i => i.DeliveryId == id).ToListAsync();
        _context.Interviews.RemoveRange(interviews);

        var aiScores = await _context.AIScores.Where(s => s.DeliveryId == id).ToListAsync();
        _context.AIScores.RemoveRange(aiScores);

        var aiQuestions = await _context.AIInterviewQuestions.Where(q => q.DeliveryId == id).ToListAsync();
        _context.AIInterviewQuestions.RemoveRange(aiQuestions);

        // 删除投递记录（HR淘汰或候选人取消）
        _context.Deliveries.Remove(delivery);
        await _context.SaveChangesAsync();
    }

    public async Task<DeliveryResponse> UpdateDeliveryInfoAsync(int id, DeliveryFormData data)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Candidate)
            .Include(d => d.Job)
            .FirstOrDefaultAsync(d => d.DeliveryId == id);
        
        if (delivery == null) throw new Exception("投递记录不存在");

        // 只更新当前投递记录的快照，不影响其他投递记录
        delivery.ContactName = data.CandidateName;
        delivery.ContactPhone = data.Phone;
        delivery.ContactEmail = data.Email;
        delivery.ContactEducation = data.Education;
        delivery.ContactWorkYears = data.WorkYears;
        if (!string.IsNullOrEmpty(data.ResumeUrl))
        {
            delivery.ContactResumeUrl = data.ResumeUrl;
        }

        delivery.UpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();

        return new DeliveryResponse(
            delivery.DeliveryId, delivery.JobId, delivery.Job!.Title, delivery.CandidateId,
            delivery.ContactName, delivery.ContactPhone, delivery.ContactEmail,
            delivery.ContactEducation, delivery.ContactWorkYears, delivery.ContactResumeUrl,
            delivery.Status, delivery.HrId, delivery.DeliverTime, delivery.UpdateTime, delivery.Remark,
            delivery.AllowAIInterview, delivery.AIInterviewDeadline);
    }

    public async Task SetAIInterviewPermissionAsync(int id, bool allow, DateTime? deadline = null)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Candidate)
            .Include(d => d.Job)
            .FirstOrDefaultAsync(d => d.DeliveryId == id);
        if (delivery == null) throw new Exception("投递记录不存在");

        delivery.AllowAIInterview = allow;
        delivery.AIInterviewDeadline = deadline;
        delivery.UpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();

        // ═══ 发送通知给候选人 ═══
        if (allow && delivery.Candidate?.UserId.HasValue == true)
        {
            var candidateUserId = delivery.Candidate.UserId.Value;
            var jobTitle = delivery.Job?.Title ?? "";
            var title = "AI面试邀请";
            var content = $"恭喜！您获得了「{jobTitle}」岗位的AI面试资格。\n" +
                          $"请登录系统在「我的投递」中查看并完成AI面试。\n" +
                          (deadline.HasValue ? $"请在 {deadline:yyyy-MM-dd HH:mm} 前完成面试。" : "");

            // 尝试发送通知（如果通知服务可用）
            try
            {
                var notificationService = _serviceProvider.GetService<INotificationService>();
                if (notificationService != null)
                {
                    await notificationService.CreateAsync(candidateUserId, "interview",
                        title, content, delivery.DeliveryId, "delivery");
                }
            }
            catch { /* 通知服务可能未注册 */ }

            // 尝试发送 SignalR 实时推送
            try
            {
                var signalR = _serviceProvider.GetService<ISignalRService>();
                if (signalR != null)
                {
                    await signalR.SendToUserAsync(candidateUserId, "AIInterviewInvited", new
                    {
                        deliveryId = delivery.DeliveryId,
                        jobTitle = delivery.Job?.Title ?? "",
                        deadline = deadline?.ToString("yyyy-MM-dd HH:mm")
                    });
                }
            }
            catch { /* SignalR 可能未连接 */ }
        }
    }

    public async Task SaveResumeTextAsync(int id, string resumeText)
    {
        var delivery = await _context.Deliveries.FindAsync(id);
        if (delivery == null) throw new Exception("投递记录不存在");
        delivery.ResumeText = resumeText;
        await _context.SaveChangesAsync();
    }

    public async Task SaveResumeFilePathAsync(int id, string filePath)
    {
        var delivery = await _context.Deliveries.FindAsync(id);
        if (delivery == null) throw new Exception("投递记录不存在");
        delivery.ContactResumeUrl = filePath;
        await _context.SaveChangesAsync();
    }

    public async Task StartInternshipAsync(int id, StartInternshipRequest req, int operatedBy)
    {
        var delivery = await _context.Deliveries.Include(d => d.Job).FirstOrDefaultAsync(d => d.DeliveryId == id);
        if (delivery == null) throw new Exception("投递记录不存在");
        if (delivery.Status != 2) throw new Exception("当前状态不是【面试中】，无法开始实习");

        delivery.Status = 3;
        delivery.UpdateTime = DateTime.Now;
        delivery.Remark = $"实习岗位：{req.Position ?? delivery.Job?.Title}；开始日期：{req.StartDate:yyyy-MM-dd}；导师：{req.Mentor ?? "-"}";

        _context.SysOperLogs.Add(new SysOperLog
        {
            UserId = operatedBy,
            Module = "Delivery",
            Action = "StartInternship",
            Detail = $"DeliveryId={id} 开始实习",
            CreatedAt = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }

    public async Task FormalHireAsync(int id, FormalHireRequest req, int operatedBy)
    {
        var delivery = await _context.Deliveries.Include(d => d.Job).FirstOrDefaultAsync(d => d.DeliveryId == id);
        if (delivery == null) throw new Exception("投递记录不存在");
        if (delivery.Status != 3) throw new Exception("当前状态不是【实习中】，无法转正");

        delivery.Status = 4;
        delivery.UpdateTime = DateTime.Now;
        delivery.Remark = $"正式职位：{req.Position ?? delivery.Job?.Title}；入职日期：{req.HireDate:yyyy-MM-dd}；薪资：{req.Salary?.ToString() ?? "-"}K";

        _context.SysOperLogs.Add(new SysOperLog
        {
            UserId = operatedBy,
            Module = "Delivery",
            Action = "FormalHire",
            Detail = $"DeliveryId={id} 正式入职",
            CreatedAt = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }
}
