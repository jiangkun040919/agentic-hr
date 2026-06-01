using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIRecruitment.Api.Models;

[Table("WorkflowDefinition")]
public class WorkflowDefinition
{
    [Key]
    public int DefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StepsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("WorkflowInstance")]
public class WorkflowInstance
{
    [Key]
    public int InstanceId { get; set; }
    public int DefinitionId { get; set; }
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public WorkflowDefinition? Definition { get; set; }
    public List<WorkflowStepLog> StepLogs { get; set; } = new();
}

[Table("WorkflowStepLog")]
public class WorkflowStepLog
{
    [Key]
    public int LogId { get; set; }
    public int InstanceId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? ResultJson { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public WorkflowInstance? Instance { get; set; }
}
