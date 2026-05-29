using Microsoft.EntityFrameworkCore;
using AIRecruitment.Api.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<SysUser> SysUsers { get; set; }
    public DbSet<Job> Jobs { get; set; }
    public DbSet<Candidate> Candidates { get; set; }
    public DbSet<Delivery> Deliveries { get; set; }
    public DbSet<Interview> Interviews { get; set; }
    public DbSet<AIScore> AIScores { get; set; }
    public DbSet<AIInterviewQuestion> AIInterviewQuestions { get; set; }
    public DbSet<AIResumeAnalysis> AIResumeAnalyses { get; set; }
    public DbSet<AIRecruitmentInsight> AIRecruitmentInsights { get; set; }
    public DbSet<SysLoginLog> SysLoginLogs { get; set; }
    public DbSet<SysOperLog> SysOperLogs { get; set; }
    public DbSet<SysConfig> SysConfigs { get; set; }
    public DbSet<UploadFile> UploadFiles { get; set; }
    public DbSet<AIInterviewSession> AIInterviewSessions { get; set; }
    public DbSet<AIInterviewMessage> AIInterviewMessages { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<GraphSnapshot> GraphSnapshots { get; set; }
    public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
    public DbSet<WorkflowInstance> WorkflowInstances { get; set; }
    public DbSet<WorkflowStepLog> WorkflowStepLogs { get; set; }
    public DbSet<SeedTemplate> SeedTemplates { get; set; }
    public DbSet<DiscoveredJob> DiscoveredJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 索引
        modelBuilder.Entity<Job>()
            .HasIndex(j => new { j.HrId, j.Status });

        modelBuilder.Entity<Delivery>()
            .HasIndex(d => new { d.JobId, d.CandidateId })
            .IsUnique();

        modelBuilder.Entity<Delivery>()
            .HasIndex(d => d.Status);

        modelBuilder.Entity<Interview>()
            .HasIndex(i => i.ScheduleTime);

        modelBuilder.Entity<SysUser>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // 显式指定表名为单数（与数据库实际表名一致）
        modelBuilder.Entity<AIInterviewSession>().ToTable("AIInterviewSessions");
        modelBuilder.Entity<AIInterviewMessage>().ToTable("AIInterviewMessages");

        // 禁用级联删除，避免 SQL Server 多路径级联冲突
        modelBuilder.Entity<Job>()
            .HasOne(j => j.Hr)
            .WithMany()
            .HasForeignKey(j => j.HrId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Delivery>()
            .HasOne(d => d.Job)
            .WithMany(j => j.Deliveries)
            .HasForeignKey(d => d.JobId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Delivery>()
            .HasOne(d => d.Candidate)
            .WithMany(c => c.Deliveries)
            .HasForeignKey(d => d.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Delivery>()
            .HasOne(d => d.Hr)
            .WithMany()
            .HasForeignKey(d => d.HrId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Interview>()
            .HasOne(i => i.Delivery)
            .WithMany(d => d.Interviews)
            .HasForeignKey(i => i.DeliveryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Interview>()
            .HasOne(i => i.Interviewer)
            .WithMany()
            .HasForeignKey(i => i.InterviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AIScore>()
            .HasOne(a => a.Delivery)
            .WithOne(d => d.AIScore)
            .HasForeignKey<AIScore>(a => a.DeliveryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AIInterviewQuestion>()
            .HasOne(a => a.Delivery)
            .WithMany()
            .HasForeignKey(a => a.DeliveryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AIInterviewQuestion>()
            .HasOne(a => a.Job)
            .WithMany()
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AIResumeAnalysis>()
            .HasOne(a => a.Candidate)
            .WithMany(c => c.AIAnalyses)
            .HasForeignKey(a => a.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AIResumeAnalysis>()
            .HasOne(a => a.Delivery)
            .WithMany()
            .HasForeignKey(a => a.DeliveryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AIRecruitmentInsight>()
            .HasOne(a => a.Hr)
            .WithMany()
            .HasForeignKey(a => a.HrId)
            .OnDelete(DeleteBehavior.Restrict);

        // AI面试会话
        modelBuilder.Entity<AIInterviewSession>()
            .HasOne(s => s.Delivery)
            .WithMany()
            .HasForeignKey(s => s.DeliveryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AIInterviewSession>()
            .HasOne(s => s.Candidate)
            .WithMany()
            .HasForeignKey(s => s.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AIInterviewSession>()
            .HasOne(s => s.Job)
            .WithMany()
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AIInterviewSession>()
            .HasMany(s => s.Messages)
            .WithOne(m => m.Session)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // 工作流引擎
        modelBuilder.Entity<WorkflowInstance>()
            .HasIndex(i => new { i.EntityId, i.EntityType });

        modelBuilder.Entity<WorkflowInstance>()
            .HasIndex(i => i.Status);

        modelBuilder.Entity<WorkflowStepLog>()
            .HasIndex(l => l.InstanceId);

        modelBuilder.Entity<SeedTemplate>()
            .HasIndex(t => t.Name)
            .IsUnique();

        modelBuilder.Entity<DiscoveredJob>()
            .HasIndex(d => d.Status);

        modelBuilder.Entity<WorkflowInstance>()
            .HasOne(i => i.Definition)
            .WithMany()
            .HasForeignKey(i => i.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkflowStepLog>()
            .HasOne(l => l.Instance)
            .WithMany(i => i.StepLogs)
            .HasForeignKey(l => l.InstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
