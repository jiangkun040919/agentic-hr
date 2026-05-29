using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIRecruitment.Api.Models;

[Table("SysUser")]
public class SysUser
{
    [Key]
    public int UserId { get; set; }
    [Required]
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? RealName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLogin { get; set; }
}

[Table("Job")]
public class Job
{
    [Key]
    public int JobId { get; set; }
    [Required]
    public string Title { get; set; } = string.Empty;
    public string Dept { get; set; } = string.Empty;
    public string JD { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public int? SalaryMin { get; set; }
    public int? SalaryMax { get; set; }
    public string Location { get; set; } = string.Empty;
    public int? HeadCount { get; set; }
    public int Status { get; set; } = 1;
    public int HrId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }

    [ForeignKey("HrId")]
    public SysUser? Hr { get; set; }
    public ICollection<Delivery>? Deliveries { get; set; }
}

[Table("Candidate")]
public class Candidate
{
    [Key]
    public int CandidateId { get; set; }
    public int? UserId { get; set; }
    [Required]
    public string RealName { get; set; } = string.Empty;
    [Required]
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Education { get; set; }
    public int? WorkYears { get; set; }
    public string? ResumeUrl { get; set; }
    /// <summary>在线简历内容（候选人手写的简历文本）</summary>
    public string? ResumeContent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Delivery>? Deliveries { get; set; }
    public ICollection<AIResumeAnalysis>? AIAnalyses { get; set; }
}

[Table("Delivery")]
public class Delivery
{
    [Key]
    public int DeliveryId { get; set; }
    public int JobId { get; set; }
    public int CandidateId { get; set; }
    /// <summary>投递时填写的姓名（快照，不受后续修改影响）</summary>
    public string ContactName { get; set; } = string.Empty;
    /// <summary>投递时填写的手机号（快照）</summary>
    public string ContactPhone { get; set; } = string.Empty;
    /// <summary>投递时填写的邮箱（快照）</summary>
    public string? ContactEmail { get; set; }
    /// <summary>投递时填写的学历（快照）</summary>
    public string? ContactEducation { get; set; }
    /// <summary>投递时填写的工作年限（快照）</summary>
    public int? ContactWorkYears { get; set; }
    /// <summary>投递时上传的简历URL（快照）</summary>
    public string? ContactResumeUrl { get; set; }
    /// <summary>PDF简历提取的文本内容</summary>
    public string? ResumeText { get; set; }
    public int Status { get; set; } = 0;
    public int HrId { get; set; }
    public DateTime DeliverTime { get; set; } = DateTime.Now;
    public DateTime? UpdateTime { get; set; }
    public string? Remark { get; set; }
    /// <summary>是否允许进行AI面试（HR手动控制）</summary>
    public bool AllowAIInterview { get; set; } = false;
    /// <summary>AI面试允许时间（可设置截止时间）</summary>
    public DateTime? AIInterviewDeadline { get; set; }

    [ForeignKey("JobId")]
    public Job? Job { get; set; }
    [ForeignKey("CandidateId")]
    public Candidate? Candidate { get; set; }
    [ForeignKey("HrId")]
    public SysUser? Hr { get; set; }
    public ICollection<Interview>? Interviews { get; set; }
    public AIScore? AIScore { get; set; }
}

[Table("Interview")]
public class Interview
{
    [Key]
    public int InterviewId { get; set; }
    public int DeliveryId { get; set; }
    public int InterviewerId { get; set; }
    public DateTime ScheduleTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public int Status { get; set; } = 0;
    public string? Result { get; set; }
    public string? Record { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("DeliveryId")]
    public Delivery? Delivery { get; set; }
    [ForeignKey("InterviewerId")]
    public SysUser? Interviewer { get; set; }
}

[Table("AIScore")]
public class AIScore
{
    [Key]
    public int ScoreId { get; set; }
    public int DeliveryId { get; set; }
    public int MatchScore { get; set; }
    public string? MatchReason { get; set; }
    public string? AnalysisReport { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("DeliveryId")]
    public Delivery? Delivery { get; set; }
}

[Table("AIInterviewQuestion")]
public class AIInterviewQuestion
{
    [Key]
    public int QuestionId { get; set; }
    public int DeliveryId { get; set; }
    public int JobId { get; set; }
    public string QuestionsJson { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("DeliveryId")]
    public Delivery? Delivery { get; set; }
    [ForeignKey("JobId")]
    public Job? Job { get; set; }
}

[Table("AIResumeAnalysis")]
public class AIResumeAnalysis
{
    [Key]
    public int AnalysisId { get; set; }
    public int DeliveryId { get; set; }
    public int CandidateId { get; set; }
    public string ParsedJson { get; set; } = string.Empty;
    public string? SkillsTags { get; set; }
    public string? WorkExperience { get; set; }
    public string? Projects { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("DeliveryId")]
    public Delivery? Delivery { get; set; }
    [ForeignKey("CandidateId")]
    public Candidate? Candidate { get; set; }
}

[Table("AIRecruitmentInsights")]
public class AIRecruitmentInsight
{
    [Key]
    public int InsightId { get; set; }
    public int HrId { get; set; }
    public string Period { get; set; } = string.Empty;
    public string PipelineData { get; set; } = string.Empty;
    public string? Recommendations { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("HrId")]
    public SysUser? Hr { get; set; }
}

[Table("SysLoginLog")]
public class SysLoginLog
{
    [Key]
    public int LogId { get; set; }
    public int UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Table("SysOperLog")]
public class SysOperLog
{
    [Key]
    public int LogId { get; set; }
    public int UserId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Table("SysConfig")]
public class SysConfig
{
    [Key]
    public int ConfigId { get; set; }
    [Required]
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public string? Description { get; set; }
}

[Table("UploadFile")]
public class UploadFile
{
    [Key]
    public int FileId { get; set; }
    [Required]
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Table("GraphSnapshot")]
public class GraphSnapshot
{
    [Key]
    public int SnapshotId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string SkillsJson { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Table("Notification")]
public class Notification
{
    [Key]
    public int NotificationId { get; set; }
    /// <summary>接收者用户ID</summary>
    public int UserId { get; set; }
    /// <summary>通知类型：interview/delivery/ai/system</summary>
    public string Type { get; set; } = "system";
    /// <summary>通知标题</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>通知内容</summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>是否已读</summary>
    public bool IsRead { get; set; } = false;
    /// <summary>关联业务ID（如面试ID、投递ID等）</summary>
    public int? RelatedId { get; set; }
    /// <summary>关联业务类型</summary>
    public string? RelatedType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ReadAt { get; set; }

    [ForeignKey("UserId")]
    public SysUser? User { get; set; }
}

[Table("SeedTemplate")]
public class SeedTemplate
{
    [Key]
    public int TemplateId { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Aliases { get; set; }                    // JSON array
    public string? Responsibilities { get; set; }           // JSON array
    public string? HardSkillsRequired { get; set; }         // JSON array
    public string? HardSkillsPreferred { get; set; }        // JSON array
    public string? SoftSkills { get; set; }                 // JSON array
    [MaxLength(50)]
    public string? EducationLevel { get; set; }
    [MaxLength(100)]
    public string? EducationMajor { get; set; }
    [MaxLength(20)]
    public string? ExpJunior { get; set; }
    [MaxLength(20)]
    public string? ExpMid { get; set; }
    [MaxLength(20)]
    public string? ExpSenior { get; set; }
    public string? Certifications { get; set; }             // JSON array
    public string? SearchKeywords { get; set; }             // JSON array
    public string? ExcludeKeywords { get; set; }            // JSON array
    public string? SourcePlatforms { get; set; }            // JSON array
    public int MaxInstances { get; set; } = 5;
    public int CurrentInstances { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
}

[Table("DiscoveredJob")]
public class DiscoveredJob
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? RawDescription { get; set; }
    [MaxLength(50)]
    public string? SourcePlatform { get; set; }
    public int? MatchedTemplateId { get; set; }
    public double? SimilarityScore { get; set; }
    [MaxLength(20)]
    public string Status { get; set; } = "pending";         // pending / approved / rejected
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

