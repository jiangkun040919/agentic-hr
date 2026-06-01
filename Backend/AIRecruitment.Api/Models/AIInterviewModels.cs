using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIRecruitment.Api.Models;

[Table("AIInterviewSession")]
public class AIInterviewSession
{
    [Key]
    public int SessionId { get; set; }

    /// <summary>关联的投递记录</summary>
    public int DeliveryId { get; set; }

    /// <summary>候选人ID</summary>
    public int CandidateId { get; set; }

    /// <summary>岗位ID</summary>
    public int JobId { get; set; }

    /// <summary>面试状态: 0=未开始, 1=进行中, 2=已完成, 3=已中断</summary>
    public int Status { get; set; } = 0;

    /// <summary>面试总时长（秒）</summary>
    public int TotalDuration { get; set; } = 0;

    /// <summary>AI综合评分(0-100)</summary>
    public int? TotalScore { get; set; }

    /// <summary>各维度评分JSON</summary>
    public string? ScoresJson { get; set; }

    /// <summary>面试开始时间</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>面试结束时间</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>面试对话JSON（完整记录）</summary>
    public string? TranscriptJson { get; set; }

    [ForeignKey("DeliveryId")]
    public Delivery? Delivery { get; set; }

    [ForeignKey("CandidateId")]
    public Candidate? Candidate { get; set; }

    [ForeignKey("JobId")]
    public Job? Job { get; set; }

    public ICollection<AIInterviewMessage>? Messages { get; set; }
}

[Table("AIInterviewMessage")]
public class AIInterviewMessage
{
    [Key]
    public int MessageId { get; set; }

    /// <summary>所属面试会话</summary>
    public int SessionId { get; set; }

    /// <summary>角色: ai / candidate</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>消息内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>消息类型: question / answer / evaluation / system</summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>AI对该条回答的评分(0-100)</summary>
    public int? Score { get; set; }

    /// <summary>AI评价内容</summary>
    public string? Evaluation { get; set; }

    /// <summary>消息顺序</summary>
    public int OrderIndex { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("SessionId")]
    public AIInterviewSession? Session { get; set; }
}
