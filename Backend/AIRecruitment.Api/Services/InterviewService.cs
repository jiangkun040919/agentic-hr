using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Models.DTOs;

namespace AIRecruitment.Api.Services;

public interface IInterviewService
{
    Task<PagedResponse<InterviewResponse>> GetInterviewListAsync(InterviewListParams p);
    Task<InterviewResponse> GetInterviewDetailAsync(int id);
    Task<InterviewResponse> ScheduleInterviewAsync(InterviewFormData data);
    Task UpdateInterviewAsync(int id, UpdateInterviewRequest data);
    Task UpdateInterviewStatusAsync(int id, int status);
    Task RecordResultAsync(int id, string result, string record);
    Task CancelInterviewAsync(int id);
    Task SendNotificationAsync(int interviewId, string[] channels);
    Task<bool> CheckConflictAsync(int interviewerId, DateTime scheduleTime);
}

public class InterviewService : IInterviewService
{
    private readonly AppDbContext _context;
    private readonly ISignalRService _signalR;
    private readonly INotificationService _notificationService;

    public InterviewService(AppDbContext context, ISignalRService signalR, INotificationService notificationService)
    {
        _context = context;
        _signalR = signalR;
        _notificationService = notificationService;
    }

    public async Task<PagedResponse<InterviewResponse>> GetInterviewListAsync(InterviewListParams p)
    {
        var query = _context.Interviews
            .Include(i => i.Delivery)
                .ThenInclude(d => d.Candidate)
            .Include(i => i.Delivery.Job)
            .Include(i => i.Interviewer)
            .AsQueryable();

        if (p.InterviewerId.HasValue)
            query = query.Where(i => i.InterviewerId == p.InterviewerId);
        if (p.Status.HasValue)
            query = query.Where(i => i.Status == p.Status);
        if (!string.IsNullOrEmpty(p.Keyword))
            query = query.Where(i => i.Delivery.Candidate.RealName.Contains(p.Keyword));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(i => i.ScheduleTime)
            .Skip((p.Page - 1) * p.PageSize)
            .Take(p.PageSize)
            .Select(i => new InterviewResponse(
                i.InterviewId, i.DeliveryId, i.Delivery.Candidate.RealName,
                i.Delivery.Job.Title, i.InterviewerId, i.Interviewer.RealName ?? "未指定",
                i.ScheduleTime, i.Location, i.Status, i.Result, i.Record, i.CreatedAt))
            .ToListAsync();

        return new PagedResponse<InterviewResponse>(items, total, p.Page, p.PageSize);
    }

    public async Task<InterviewResponse> GetInterviewDetailAsync(int id)
    {
        var interview = await _context.Interviews
            .Include(i => i.Delivery).ThenInclude(d => d.Candidate)
            .Include(i => i.Delivery.Job)
            .Include(i => i.Interviewer)
            .FirstOrDefaultAsync(i => i.InterviewId == id);

        if (interview == null) throw new Exception("面试记录不存在");

        return new InterviewResponse(
            interview.InterviewId, interview.DeliveryId, interview.Delivery.Candidate.RealName,
            interview.Delivery.Job.Title, interview.InterviewerId, interview.Interviewer.RealName ?? "未指定",
            interview.ScheduleTime, interview.Location, interview.Status,
            interview.Result, interview.Record, interview.CreatedAt);
    }

    public async Task<InterviewResponse> ScheduleInterviewAsync(InterviewFormData data)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Candidate)
            .Include(d => d.Job)
            .FirstOrDefaultAsync(d => d.DeliveryId == data.DeliveryId);
        if (delivery == null) throw new Exception("投递记录不存在");
        
        delivery.Status = 2;
        delivery.UpdateTime = DateTime.UtcNow;

        var interviewer = await _context.SysUsers.FindAsync(data.InterviewerId);
        int actualInterviewerId = interviewer != null ? data.InterviewerId : delivery.HrId;

        var interview = new Interview
        {
            DeliveryId = data.DeliveryId,
            InterviewerId = actualInterviewerId,
            ScheduleTime = data.ScheduleTime,
            Location = data.Location,
            Status = 0,
            CreatedAt = DateTime.UtcNow
        };

        _context.Interviews.Add(interview);
        await _context.SaveChangesAsync();

        // 发送站内消息通知
        var candidateName = delivery.Candidate?.RealName ?? "候选人";
        var jobTitle = delivery.Job?.Title ?? "";
        var notifyContent = $"恭喜您获得面试邀请！\n应聘岗位：{jobTitle}\n面试时间：{data.ScheduleTime:yyyy-MM-dd HH:mm}\n面试形式：{data.Location}\n如有疑问请联系HR。";

        // 通知候选人（如果有绑定用户）
        if (delivery.Candidate?.UserId.HasValue == true)
        {
            await _notificationService.CreateAsync(
                delivery.Candidate.UserId.Value, "interview",
                "面试邀请通知", notifyContent,
                interview.InterviewId, "interview");
        }

        // 通知面试官
        await _notificationService.CreateAsync(
            actualInterviewerId, "interview",
            $"新的面试安排：{candidateName} - {jobTitle}",
            $"候选人：{candidateName}\n岗位：{jobTitle}\n时间：{data.ScheduleTime:yyyy-MM-dd HH:mm}\n地点：{data.Location}",
            interview.InterviewId, "interview");

        await _signalR.SendToUserAsync(delivery.HrId, "InterviewScheduled", new
        {
            time = data.ScheduleTime.ToString("yyyy-MM-dd HH:mm"),
            location = data.Location
        });

        return new InterviewResponse(
            interview.InterviewId, interview.DeliveryId, delivery.Candidate?.RealName ?? "",
            delivery.Job?.Title ?? "", data.InterviewerId, interviewer?.RealName ?? "未指定",
            interview.ScheduleTime, interview.Location,
            interview.Status, null, null, interview.CreatedAt);
    }

    public async Task UpdateInterviewAsync(int id, UpdateInterviewRequest data)
    {
        var interview = await _context.Interviews.FindAsync(id);
        if (interview == null) throw new Exception("面试记录不存在");

        if (data.ScheduleTime.HasValue)
            interview.ScheduleTime = data.ScheduleTime.Value;
        if (!string.IsNullOrEmpty(data.InterviewType))
            interview.Location = data.InterviewType == "线上面试" ? "线上会议"
                : data.InterviewType == "现场面试" ? "公司总部" : "电话面试";
        if (data.InterviewerId.HasValue)
            interview.InterviewerId = data.InterviewerId.Value;
        if (!string.IsNullOrEmpty(data.Remark))
            interview.Result = data.Remark;

        interview.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateInterviewStatusAsync(int id, int status)
    {
        var interview = await _context.Interviews.FindAsync(id);
        if (interview == null) throw new Exception("面试记录不存在");

        interview.Status = status;
        interview.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task RecordResultAsync(int id, string result, string record)
    {
        var interview = await _context.Interviews.FindAsync(id);
        if (interview == null) throw new Exception("面试记录不存在");

        interview.Result = result;
        interview.Record = record;
        interview.Status = result == "通过" ? 2 : (result == "不通过" ? 3 : 1);
        interview.UpdatedAt = DateTime.UtcNow;

        var delivery = await _context.Deliveries.FindAsync(interview.DeliveryId);
        if (delivery != null)
        {
            delivery.Status = result == "通过" ? 3 : 4;
            delivery.UpdateTime = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task CancelInterviewAsync(int id)
    {
        var interview = await _context.Interviews.FindAsync(id);
        if (interview == null) throw new Exception("面试记录不存在");

        interview.Status = 4;
        interview.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task SendNotificationAsync(int interviewId, string[] channels)
    {
        var interview = await _context.Interviews
            .Include(i => i.Delivery).ThenInclude(d => d.Candidate)
            .Include(i => i.Delivery).ThenInclude(d => d.Job)
            .FirstOrDefaultAsync(i => i.InterviewId == interviewId);
        if (interview == null)
            return; // 静默返回，不抛异常

        var candidateUserId = interview.Delivery?.Candidate?.UserId;
        if (candidateUserId.HasValue && candidateUserId.Value > 0)
        {
            try
            {
                await _notificationService.CreateAsync(candidateUserId.Value, "interview",
                    "面试通知", $"您的面试安排在 {interview.ScheduleTime:yyyy-MM-dd HH:mm}，请准时参加。",
                    interviewId, "interview");
            }
            catch { /* 静默失败 */ }
        }
    }

    public async Task<bool> CheckConflictAsync(int interviewerId, DateTime scheduleTime)
    {
        var conflict = await _context.Interviews.AnyAsync(i =>
            i.InterviewerId == interviewerId &&
            i.Status <= 1 &&
            i.ScheduleTime >= scheduleTime.AddHours(-1) &&
            i.ScheduleTime <= scheduleTime.AddHours(1));
        return conflict;
    }
}