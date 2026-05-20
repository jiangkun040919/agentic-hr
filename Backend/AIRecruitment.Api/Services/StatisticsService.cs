using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Models.DTOs;

namespace AIRecruitment.Api.Services;

public interface IStatisticsService
{
    Task<DashboardResponse> GetDashboardDataAsync(int? hrId);
    Task<FunnelDataResponse> GetFunnelDataAsync(int hrId, DateTime? startDate, DateTime? endDate);
    Task<List<JobStatItem>> GetJobStatsAsync(int hrId, DateTime? startDate, DateTime? endDate);
    Task<List<SourceStatItem>> GetResumeSourceStatsAsync(DateTime? startDate, DateTime? endDate);
    Task<List<TrendItem>> GetTrendDataAsync(int days, string? type);
    Task<FlowPoolResponse> GetFlowPoolDataAsync(int hrId);
    Task<MultiTrendResponse> GetMultiTrendDataAsync(string dimension);
    Task<List<HireRateItem>> GetHireRateDataAsync(int hrId, string dimension);
    Task<List<HotJobDetail>> GetHotJobDetailsAsync(int hrId);
}

public class StatisticsService : IStatisticsService
{
    private readonly AppDbContext _context;

    public StatisticsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardResponse> GetDashboardDataAsync(int? hrId)
    {
        var now = DateTime.Now;
        var today = now.Date;
        var thisWeek = today.AddDays(-(int)today.DayOfWeek);

        // 统计数据：hrId=null 表示 admin 看全量
        var openJobs = await _context.Jobs.CountAsync(j => (hrId == null || j.HrId == hrId) && j.Status == 1);
        var totalDeliveries = await _context.Deliveries.CountAsync(d => hrId == null || d.HrId == hrId);
        var interviews = await _context.Interviews.CountAsync(i => (hrId == null || i.InterviewerId == hrId) && i.ScheduleTime >= today && i.ScheduleTime < today.AddDays(1));
        var hired = await _context.Deliveries.CountAsync(d => (hrId == null || d.HrId == hrId) && (d.Status == 3 || d.Status == 4));

        var stats = new Dictionary<string, int>
        {
            { "openJobs", openJobs },
            { "totalDeliveries", totalDeliveries },
            { "interviews", interviews },
            { "hired", hired }
        };

        // 待处理简历（状态为待查看或已查看，不包含已安排面试的）
        var pendingResumesData = await _context.Deliveries
            .Include(d => d.Job)
            .Include(d => d.Candidate)
            .Where(d => (hrId == null || d.HrId == hrId) && d.Status < 2)
            .OrderByDescending(d => d.DeliverTime)
            .ToListAsync();
        
        var pendingResumes = pendingResumesData.Select(d => {
            bool has = !string.IsNullOrEmpty(d.ContactPhone);
            return new DeliveryResponse(
                d.DeliveryId, d.JobId, d.Job!.Title, d.CandidateId,
                has ? d.ContactName : (d.Candidate?.RealName ?? d.ContactName),
                has ? d.ContactPhone : (d.Candidate?.Phone ?? d.ContactPhone),
                has ? d.ContactEmail : (d.Candidate?.Email ?? d.ContactEmail),
                has ? d.ContactEducation : (d.Candidate?.Education ?? d.ContactEducation),
                has ? d.ContactWorkYears : (d.Candidate?.WorkYears ?? d.ContactWorkYears),
                has ? d.ContactResumeUrl : (d.Candidate?.ResumeUrl ?? d.ContactResumeUrl),
                d.Status, d.HrId, d.DeliverTime, d.UpdateTime, d.Remark,
                d.AllowAIInterview, d.AIInterviewDeadline);
        }).ToList();

        // 今日面试
        var todayInterviews = await _context.Interviews
            .Include(i => i.Delivery).ThenInclude(d => d.Candidate)
            .Include(i => i.Delivery.Job)
            .Where(i => (hrId == null || i.InterviewerId == hrId) && i.ScheduleTime >= today && i.ScheduleTime < today.AddDays(1) && i.Status == 0)
            .OrderBy(i => i.ScheduleTime)
            .Take(5)
            .Select(i => new InterviewResponse(
                i.InterviewId, i.DeliveryId, i.Delivery.Candidate.RealName, i.Delivery.Job.Title,
                i.InterviewerId, "", i.ScheduleTime, i.Location, i.Status, null, null, i.CreatedAt))
            .ToListAsync();

        // 最近投递
        var recentDeliveriesData = await _context.Deliveries
            .Include(d => d.Job)
            .Include(d => d.Candidate)
            .Where(d => hrId == null || d.HrId == hrId)
            .OrderByDescending(d => d.DeliverTime)
            .Take(10)
            .ToListAsync();
        
        var recentDeliveries = recentDeliveriesData.Select(d => {
            bool has = !string.IsNullOrEmpty(d.ContactPhone);
            return new DeliveryResponse(
                d.DeliveryId, d.JobId, d.Job!.Title, d.CandidateId,
                has ? d.ContactName : (d.Candidate?.RealName ?? d.ContactName),
                has ? d.ContactPhone : (d.Candidate?.Phone ?? d.ContactPhone),
                has ? d.ContactEmail : (d.Candidate?.Email ?? d.ContactEmail),
                has ? d.ContactEducation : (d.Candidate?.Education ?? d.ContactEducation),
                has ? d.ContactWorkYears : (d.Candidate?.WorkYears ?? d.ContactWorkYears),
                has ? d.ContactResumeUrl : (d.Candidate?.ResumeUrl ?? d.ContactResumeUrl),
                d.Status, d.HrId, d.DeliverTime, d.UpdateTime, d.Remark,
                d.AllowAIInterview, d.AIInterviewDeadline);
        }).ToList();

        return new DashboardResponse(stats, pendingResumes, todayInterviews, recentDeliveries);
    }

    public async Task<FunnelDataResponse> GetFunnelDataAsync(int hrId, DateTime? startDate, DateTime? endDate)
    {
        var query = _context.Deliveries.Where(d => d.HrId == hrId);
        if (startDate.HasValue) query = query.Where(d => d.DeliverTime >= startDate);
        if (endDate.HasValue) query = query.Where(d => d.DeliverTime <= endDate);

        var total = await query.CountAsync();
        var reviewed = await query.CountAsync(d => d.Status >= 1);
        var interviewed = await query.CountAsync(d => d.Status >= 2);
        var oneInterview = await query.CountAsync(d => d.Status >= 2);
        var hired = await query.CountAsync(d => d.Status == 4);

        var data = new List<FunnelItem>
        {
            new("投递简历", total),
            new("简历筛选", reviewed),
            new("面试邀请", interviewed),
            new("一面", oneInterview),
            new("正式入职", hired)
        };

        return new FunnelDataResponse(data);
    }

    public async Task<List<JobStatItem>> GetJobStatsAsync(int hrId, DateTime? startDate, DateTime? endDate)
    {
        var query = _context.Deliveries
            .Include(d => d.Job)
            .AsQueryable();

        if (hrId > 0) query = query.Where(d => d.HrId == hrId);
        if (startDate.HasValue) query = query.Where(d => d.DeliverTime >= startDate);
        if (endDate.HasValue) query = query.Where(d => d.DeliverTime <= endDate);

        // Use client-side grouping to avoid EF Core translation issues
        var deliveries = await query.ToListAsync();
        var stats = deliveries
            .GroupBy(d => d.Job?.Title ?? "未知岗位")
            .Select(g => new JobStatItem(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        return stats;
    }

    public async Task<List<SourceStatItem>> GetResumeSourceStatsAsync(DateTime? startDate, DateTime? endDate)
    {
        var query = _context.Deliveries.AsQueryable();
        if (startDate.HasValue) query = query.Where(d => d.DeliverTime >= startDate);
        if (endDate.HasValue) query = query.Where(d => d.DeliverTime <= endDate);

        // 模拟简历来源统计
        var sources = new List<SourceStatItem>
        {
            new("招聘网站", 45),
            new("内部推荐", 25),
            new("社交媒体", 15),
            new("校园招聘", 10),
            new("其他", 5)
        };

        return sources;
    }

    public async Task<List<TrendItem>> GetTrendDataAsync(int days, string? type)
    {
        var trends = new List<TrendItem>();
        var now = DateTime.Now;

        for (int i = days - 1; i >= 0; i--)
        {
            var date = now.AddDays(-i).ToString("MM-dd");
            var value = new Random().Next(5, 20);
            trends.Add(new TrendItem(date, value));
        }

        return trends;
    }

    public async Task<MultiTrendResponse> GetMultiTrendDataAsync(string dimension)
    {
        var deliveryCount = await _context.Deliveries.CountAsync();
        var interviewCount = await _context.Interviews.CountAsync();
        var interviewPassCount = await _context.Interviews.CountAsync(i => i.Status == 2);
        var interviewFailCount = await _context.Interviews.CountAsync(i => i.Status == 3);
        var internshipCount = await _context.Deliveries.CountAsync(d => d.Status == 3);
        var hireCount = await _context.Deliveries.CountAsync(d => d.Status == 4);

        return new MultiTrendResponse(
            new List<string> { "统计" },
            new List<int> { deliveryCount },
            new List<int> { interviewCount },
            new List<int> { interviewPassCount },
            new List<int> { interviewFailCount },
            new List<int> { internshipCount },
            new List<int> { hireCount }
        );
    }

    public async Task<FlowPoolResponse> GetFlowPoolDataAsync(int hrId)
    {
        var deliveryList = await _context.Deliveries
            .Include(d => d.Candidate)
            .Include(d => d.Job)
            .OrderByDescending(d => d.DeliverTime)
            .Select(d => new FlowPersonItem(
                d.DeliveryId.ToString(),
                d.Candidate.RealName,
                d.Job.Title,
                d.DeliverTime.ToString("yyyy-MM-dd HH:mm"),
                d.Status == 0 ? "待查看" :
                d.Status == 1 ? "已查看" :
                d.Status == 2 ? "面试中" :
                d.Status == 3 ? "实习中" :
                d.Status == 4 ? "正式入职" :
                d.Status == 5 ? "已淘汰" : "未知",
                d.Candidate.Email ?? "",
                d.Candidate.Phone,
                d.Candidate.Education ?? "",
                d.Candidate.WorkYears.HasValue ? d.Candidate.WorkYears.Value + "年" : "",
                "",
                new List<string>(),
                ""
            ))
            .ToListAsync();

        var interviewList = await _context.Interviews
            .Include(i => i.Delivery).ThenInclude(d => d.Candidate)
            .Include(i => i.Delivery.Job)
            .Where(i => i.Status < 2)
            .OrderBy(i => i.ScheduleTime)
            .Select(i => new FlowPersonItem(
                i.InterviewId.ToString(),
                i.Delivery.Candidate.RealName,
                i.Delivery.Job.Title,
                i.ScheduleTime.ToString("yyyy-MM-dd HH:mm"),
                i.Status == 0 ? "待面试" : "面试中",
                i.Delivery.Candidate.Email ?? "",
                i.Delivery.Candidate.Phone,
                i.Delivery.Candidate.Education ?? "",
                i.Delivery.Candidate.WorkYears.HasValue ? i.Delivery.Candidate.WorkYears.Value + "年" : "",
                "",
                new List<string>(),
                ""
            ))
            .ToListAsync();

        var internshipList = await _context.Deliveries
            .Include(d => d.Candidate)
            .Include(d => d.Job)
            .Where(d => d.Status == 3)
            .OrderByDescending(d => d.UpdateTime)
            .Select(d => new FlowPersonItem(
                d.DeliveryId.ToString(),
                d.Candidate.RealName,
                d.Job.Title,
                d.UpdateTime.HasValue ? d.UpdateTime.Value.ToString("yyyy-MM-dd") : "",
                "实习中",
                d.Candidate.Email ?? "",
                d.Candidate.Phone,
                d.Candidate.Education ?? "",
                d.Candidate.WorkYears.HasValue ? d.Candidate.WorkYears.Value + "年" : "",
                "",
                new List<string>(),
                ""
            ))
            .ToListAsync();

        var hiredList = await _context.Deliveries
            .Include(d => d.Candidate)
            .Include(d => d.Job)
            .Where(d => d.Status == 4)
            .OrderByDescending(d => d.UpdateTime)
            .Select(d => new FlowPersonItem(
                d.DeliveryId.ToString(),
                d.Candidate.RealName,
                d.Job.Title,
                d.UpdateTime.HasValue ? d.UpdateTime.Value.ToString("yyyy-MM-dd") : "",
                "正式入职",
                d.Candidate.Email ?? "",
                d.Candidate.Phone,
                d.Candidate.Education ?? "",
                d.Candidate.WorkYears.HasValue ? d.Candidate.WorkYears.Value + "年" : "",
                "",
                new List<string>(),
                ""
            ))
            .ToListAsync();

        return new FlowPoolResponse(
            deliveryList.Count,
            interviewList.Count,
            internshipList.Count,
            hiredList.Count,
            deliveryList,
            interviewList,
            internshipList,
            hiredList
        );
    }

    public async Task<List<HireRateItem>> GetHireRateDataAsync(int hrId, string dimension)
    {
        var now = DateTime.Now;
        var result = new List<HireRateItem>();

        if (dimension == "day")
        {
            for (int i = 6; i >= 0; i--)
            {
                var date = now.AddDays(-i);
                var label = $"{date.Month.ToString().PadLeft(2, '0')}-{date.Day.ToString().PadLeft(2, '0')}";
                var startOfDay = date.Date;
                var endOfDay = startOfDay.AddDays(1);

                var total = await _context.Deliveries
                    .CountAsync(d => d.HrId == hrId && d.DeliverTime >= startOfDay && d.DeliverTime < endOfDay);
                
                var hired = await _context.Deliveries
                    .CountAsync(d => d.HrId == hrId && d.Status == 4 && d.UpdateTime >= startOfDay && d.UpdateTime < endOfDay);

                var rate = total > 0 ? Math.Round((decimal)hired / total * 100, 1) : 0;
                result.Add(new HireRateItem(label, total, hired, rate));
            }
        }
        else
        {
            for (int i = 5; i >= 0; i--)
            {
                var date = now.AddMonths(-i);
                var label = $"{date.Year}.{date.Month.ToString().PadLeft(2, '0')}";
                var startOfMonth = new DateTime(date.Year, date.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1);

                var total = await _context.Deliveries
                    .CountAsync(d => d.HrId == hrId && d.DeliverTime >= startOfMonth && d.DeliverTime < endOfMonth);
                
                var hired = await _context.Deliveries
                    .CountAsync(d => d.HrId == hrId && d.Status == 3 && d.UpdateTime >= startOfMonth && d.UpdateTime < endOfMonth);

                var rate = total > 0 ? Math.Round((decimal)hired / total * 100, 1) : 0;
                result.Add(new HireRateItem(label, total, hired, rate));
            }
        }

        return result;
    }

    public async Task<List<HotJobDetail>> GetHotJobDetailsAsync(int hrId)
    {
        var jobs = await _context.Jobs
            .Where(j => j.HrId == hrId)
            .ToListAsync();

        var result = new List<HotJobDetail>();

        foreach (var job in jobs)
        {
            var deliveries = await _context.Deliveries
                .CountAsync(d => d.JobId == job.JobId);
            
            var interviews = await _context.Interviews
                .Where(i => i.Delivery != null && i.Delivery.JobId == job.JobId)
                .CountAsync();
            
            var hires = await _context.Deliveries
                .CountAsync(d => d.JobId == job.JobId && d.Status == 3);

            var conversionRate = deliveries > 0 ? Math.Round((decimal)hires / deliveries * 100, 1) : 0;

            var status = job.Status switch
            {
                1 => "招聘中",
                2 => "待审批",
                0 => "已暂停",
                _ => "未知"
            };

            result.Add(new HotJobDetail(
                job.JobId,
                job.Title,
                deliveries,
                interviews,
                hires,
                conversionRate,
                status
            ));
        }

        return result.OrderByDescending(j => j.DeliveryCount).Take(3).ToList();
    }
}

public record JobStatItem(string JobTitle, int Count);
public record SourceStatItem(string Source, int Count);
public record TrendItem(string Date, int Value);

public record FlowPersonItem(
    string Id,
    string Name,
    string Job,
    string Time,
    string Status,
    string Email,
    string Phone,
    string Education,
    string Experience,
    string Major,
    List<string> Skills,
    string Remark
);

public record FlowPoolResponse(
    int DeliveryCount,
    int InterviewCount,
    int InternshipCount,
    int HiredCount,
    List<FlowPersonItem> DeliveryList,
    List<FlowPersonItem> InterviewList,
    List<FlowPersonItem> InternshipList,
    List<FlowPersonItem> HiredList
);

public record MultiTrendResponse(
    List<string> Labels,
    List<int> DeliveryData,
    List<int> InterviewData,
    List<int> InterviewPassData,
    List<int> InterviewFailData,
    List<int> InternshipData,
    List<int> HireData
);

public record HireRateItem(string Label, int Total, int Hired, decimal Rate);

public record HotJobDetail(
    int JobId,
    string JobTitle,
    int DeliveryCount,
    int InterviewCount,
    int HireCount,
    decimal ConversionRate,
    string Status
);