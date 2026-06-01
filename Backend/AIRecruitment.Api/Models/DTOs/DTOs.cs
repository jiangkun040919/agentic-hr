namespace AIRecruitment.Api.Models.DTOs;

// ========== 认证相关 ==========
public record LoginRequest(string Username, string Password);
public class RegisterRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "";
    public string RealName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
public record LoginResponse(string Token, string Role, int UserId, string Username);
public record UserInfoResponse(int UserId, string Username, string Role, string? RealName, string? Phone, string? Email, string? Avatar,
    string? Education = null, int? WorkYears = null, string? ResumeContent = null, string? ResumeUrl = null, int? CandidateId = null);
public record ChangePasswordRequest(string OldPassword, string NewPassword);
public record UpdateProfileRequest(string? RealName = null, string? Phone = null, string? Email = null,
    string? Education = null, int? WorkYears = null, string? ResumeContent = null, string? ResumeUrl = null);

// ========== 岗位相关 ==========
public record JobListParams(
    int Page = 1,
    int PageSize = 10,
    string? Keyword = null,
    string? Dept = null,
    string? Location = null,
    int? Status = null,
    string? SortBy = null,
    string? SortOrder = null
);

public record JobFormData(
    string Title,
    string Dept,
    string Location,
    string? JD,
    string? Requirements,
    int? SalaryMin,
    int? SalaryMax,
    int? HeadCount,
    int Status = 1,
    DateTime? ExpiredAt = null
);

public record JobResponse(
    int JobId,
    string Title,
    string Dept,
    string Location,
    string JD,
    string Requirements,
    int? SalaryMin,
    int? SalaryMax,
    int? HeadCount,
    int Status,
    int HrId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ExpiredAt,
    List<string>? Skills = null,
    int? DeliveryCount = null,
    int? InterviewCount = null
);

// ========== 投递相关 ==========
public record DeliveryListParams(
    int Page = 1,
    int PageSize = 10,
    int? JobId = null,
    int? HrId = null,
    int? Status = null,
    string? Keyword = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
);

public record DeliveryFormData(
    int JobId,
    string CandidateName,
    string Phone,
    string? Email,
    string? Education,
    int? WorkYears,
    string? ResumeUrl,
    string? ResumeJson
);

public record DeliveryResponse(
    int DeliveryId,
    int JobId,
    string JobTitle,
    int CandidateId,
    string CandidateName,
    string Phone,
    string? Email,
    string? Education,
    int? WorkYears,
    string? ResumeUrl,
    int Status,
    int HrId,
    DateTime DeliverTime,
    DateTime? UpdateTime,
    string? Remark,
    bool AllowAIInterview = false,
    DateTime? AIInterviewDeadline = null,
    string? ResumeText = null
);

// ========== 面试相关 ==========
public record InterviewListParams(
    int Page = 1,
    int PageSize = 10,
    int? DeliveryId = null,
    int? InterviewerId = null,
    int? Status = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? Keyword = null
);

public record InterviewFormData(
    int DeliveryId,
    int InterviewerId,
    DateTime ScheduleTime,
    string Location,
    string? Remark = null
);

public record UpdateInterviewRequest(
    DateTime? ScheduleTime = null,
    string? InterviewType = null,
    string? Location = null,
    int? InterviewerId = null,
    string? Remark = null,
    string? Round = null,
    int? Duration = null
);

public record InterviewResponse(
    int InterviewId,
    int DeliveryId,
    string CandidateName,
    string JobTitle,
    int InterviewerId,
    string InterviewerName,
    DateTime ScheduleTime,
    string Location,
    int Status,
    string? Result,
    string? Record,
    DateTime CreatedAt
);

// ========== 统计相关 ==========
public record DashboardResponse(
    Dictionary<string, int> Stats,
    List<DeliveryResponse> PendingResumes,
    List<InterviewResponse> TodayInterviews,
    List<DeliveryResponse> RecentDeliveries,
    List<DeptDistribution>? DeptDistribution = null
);

public record DeptDistribution(
    string Name,
    int Value
);

public record FunnelDataResponse(
    List<FunnelItem> Data
);

public record FunnelItem(string Name, int Value);

// ========== 状态更新 ==========
public record StatusUpdateRequest(int Status);

// ========== 实习 & 正式入职 ==========
public record StartInternshipRequest(DateTime? StartDate, string? Position, string? Mentor);
public record FormalHireRequest(DateTime? HireDate, string? Position, decimal? Salary);

// ========== 通用响应 ==========
public record ApiResponse<T>(bool Success, string Message, T? Data);
public record PagedResponse<T>(List<T> Items, int Total, int Page, int PageSize);

// ========== 种子模板 ==========
public record SeedTemplateListParams(int Page = 1, int PageSize = 20, string? Keyword = null, string? Category = null);

public record SeedTemplateFormData(
    string Name,
    string? Category = null,
    string? Aliases = null,
    string? Responsibilities = null,
    string? HardSkillsRequired = null,
    string? HardSkillsPreferred = null,
    string? SoftSkills = null,
    string? EducationLevel = null,
    string? EducationMajor = null,
    string? ExpJunior = null,
    string? ExpMid = null,
    string? ExpSenior = null,
    string? Certifications = null,
    string? SearchKeywords = null,
    string? ExcludeKeywords = null,
    string? SourcePlatforms = null,
    int MaxInstances = 5
);

public record DiscoveredJobListParams(int Page = 1, int PageSize = 20, string? Status = null);

public record DiscoveredJobActionRequest(string Action, string? ReviewedBy = null);

public record EnrichJobRequest(
    string Title,
    string? Jd = null,
    string? Requirements = null,
    string? Location = null,
    decimal? SalaryMin = null,
    decimal? SalaryMax = null
);

public class ClearAllJobsRequest
{
    public bool Confirm { get; set; }
}

