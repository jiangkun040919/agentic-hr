using AIRecruitment.Api.Data;
using AIRecruitment.Api.Extensions;
using AIRecruitment.Api.Hubs;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Services;
using Hangfire;
using Hangfire.Dashboard;
using System.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- 服务注册 ----
builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddAppHangfire(builder.Configuration);

var app = builder.Build();

// ---- 数据库初始化（EF Core 建表 → 补充 DDL） ----
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // 保留现有数据库，仅确保表结构存在
        db.Database.EnsureCreated();

        // 手动补列（EnsureCreated 不会给现有表加新列）
        try { db.Database.ExecuteSql($$$"""IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Candidate') AND name='ResumeContent') ALTER TABLE Candidate ADD ResumeContent NVARCHAR(MAX) NULL;"""); }
        catch (Exception ex) { Console.WriteLine($"[Startup] 补列 ResumeContent 跳过: {ex.Message}"); }

        // 补列 — AI面试权限
        try { db.Database.ExecuteSql($$$"""IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Delivery') AND name='AllowAIInterview') ALTER TABLE Delivery ADD AllowAIInterview BIT NOT NULL DEFAULT 0;"""); }
        catch (Exception ex) { Console.WriteLine($"[Startup] 补列 AllowAIInterview 跳过: {ex.Message}"); }
        try { db.Database.ExecuteSql($$$"""IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Delivery') AND name='AIInterviewDeadline') ALTER TABLE Delivery ADD AIInterviewDeadline DATETIME2 NULL;"""); }
        catch (Exception ex) { Console.WriteLine($"[Startup] 补列 AIInterviewDeadline 跳过: {ex.Message}"); }

        Console.WriteLine("[Startup] EF Core 数据库表初始化完成");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] EF Core 建表失败（不影响启动）: {ex.Message}");
    }
}
DbInitializer.EnsureTables(builder.Configuration);

// ---- 种子模板系统建表（确保新表存在） ----
using (var scope2 = app.Services.CreateScope())
{
    try
    {
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var conn = db2.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='SeedTemplate')
CREATE TABLE SeedTemplate (
    TemplateId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Category NVARCHAR(50),
    Aliases NVARCHAR(500),
    Responsibilities NVARCHAR(MAX),
    HardSkillsRequired NVARCHAR(500),
    HardSkillsPreferred NVARCHAR(500),
    SoftSkills NVARCHAR(300),
    EducationLevel NVARCHAR(50),
    EducationMajor NVARCHAR(100),
    ExpJunior NVARCHAR(20),
    ExpMid NVARCHAR(20),
    ExpSenior NVARCHAR(20),
    Certifications NVARCHAR(300),
    SearchKeywords NVARCHAR(1000),
    ExcludeKeywords NVARCHAR(500),
    SourcePlatforms NVARCHAR(500),
    MaxInstances INT DEFAULT 5,
    CurrentInstances INT DEFAULT 0,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='DiscoveredJob')
CREATE TABLE DiscoveredJob (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(200) NOT NULL,
    RawDescription NVARCHAR(MAX),
    SourcePlatform NVARCHAR(50),
    MatchedTemplateId INT,
    SimilarityScore FLOAT,
    Status NVARCHAR(20) DEFAULT 'pending',
    ReviewedBy NVARCHAR(50),
    ReviewedAt DATETIME NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
)";
        cmd.ExecuteNonQuery();
        Console.WriteLine("[Startup] 种子模板系统表已就绪");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] 种子模板表创建跳过: {ex.Message}");
    }
}

// ---- 播种演示数据（已禁用——改用种子模板系统） ----
/* 演示数据播种已禁用。使用种子模板系统手动管理岗位数据。
try
{
    using var seedScope = app.Services.CreateScope();
    var seeder = seedScope.ServiceProvider.GetRequiredService<DataSeederService>();
    await seeder.SeedAsync();
    Console.WriteLine("[Startup] 演示数据初始化完成");
}
catch (Exception ex) { Console.WriteLine($"[Startup] 数据播种跳过: {ex.Message}"); }
*/

// ---- 为面试状态投递自动生成面试记录 ----
try
{
    using var interviewSeedScope = app.Services.CreateScope();
    var interviewDb = interviewSeedScope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!interviewDb.Interviews.Any())
    {
        var interviewDeliveries = interviewDb.Deliveries
            .Where(d => d.Status >= 2)
            .OrderBy(d => d.DeliverTime)
            .ToList();
        var adminUser = interviewDb.SysUsers.FirstOrDefault(u => u.Role == "admin");
        var hrUsers = interviewDb.SysUsers.Where(u => u.Role == "hr").ToList();
        var rng = new Random();
        var locations = new[] { "线上-腾讯会议", "线上-Zoom", "会议室A", "会议室B", "公司总部" };
        var resultOptions = new[] { "通过-技术能力优秀", "通过-沟通表达好", "通过-综合素质强", "待定-需加面", "不通过-经验不足", "不通过-技能不匹配" };

        foreach (var delivery in interviewDeliveries.Take(50))
        {
            var interviewerId = hrUsers.Count > 0 ? hrUsers[rng.Next(hrUsers.Count)].UserId : adminUser.UserId;
            var daysOffset = rng.Next(1, 14);
            var status = delivery.Status switch
            {
                4 => 2, // 已入职 → 面试已通过
                3 => 2, // 实习中 → 面试已通过
                _ => rng.Next(0, 4) // 0=待面试 1=进行中 2=通过 3=不通过
            };
            var scheduleTime = delivery.DeliverTime.AddDays(rng.Next(1, 7));
            var interview = new Interview
            {
                DeliveryId = delivery.DeliveryId,
                InterviewerId = interviewerId,
                ScheduleTime = scheduleTime,
                Location = locations[rng.Next(locations.Length)],
                Status = status,
                Result = status >= 2 ? resultOptions[rng.Next(resultOptions.Length)] : null,
                Record = status >= 2 ? "面试记录：候选人表现" + (status == 2 ? "良好" : "一般") : null,
                CreatedAt = scheduleTime.AddDays(-rng.Next(1, 3))
            };
            interviewDb.Interviews.Add(interview);
        }
        await interviewDb.SaveChangesAsync();
        Console.WriteLine($"[Startup] 面试记录自动生成完成: {Math.Min(50, interviewDeliveries.Count)}条");
    }
}
catch (Exception ex) { Console.WriteLine($"[Startup] 面试记录生成跳过: {ex.Message}"); }

// ---- Hangfire 仪表盘 + 定时任务 ----
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
});
RecurringJob.AddOrUpdate<ScheduledTasksService>(
    "auto-close-expired-jobs", x => x.AutoCloseExpiredJobs(), "0 * * * *");
RecurringJob.AddOrUpdate<ScheduledTasksService>(
    "stale-candidate-reminder", x => x.RemindStaleCandidates(), "0 9 * * *");
RecurringJob.AddOrUpdate<ScheduledTasksService>(
    "weekly-recruitment-report", x => x.GenerateWeeklyReport(), "0 9 * * 1");
RecurringJob.AddOrUpdate<DataCollectionService>(
    "multi-source-data-collection", x => x.RunETLPipelineAsync(), "17 * * * *");
RecurringJob.RemoveIfExists("multi-source-data-collection");

RecurringJob.AddOrUpdate<ScheduledTasksService>(
    "graph-snapshot-weekly", x => x.TakeGraphSnapshotAsync(), "0 2 * * 1");

// ═══ 实时岗位采集已禁用（改用种子模板驱动爬取）═══
// RecurringJob.AddOrUpdate<RealtimeJobCollectorService>(
//     "realtime-job-collection", x => x.CollectAsync(), "0 */4 * * *");
RecurringJob.RemoveIfExists("realtime-job-collection");

// ═══ 图谱自演化（每周自动运行，真实数据驱动）═══
RecurringJob.AddOrUpdate<GraphEvolutionService>(
    "graph-self-evolution", x => x.RunEvolutionCycleAsync(), "0 3 * * 1");

RecurringJob.AddOrUpdate<IDailyStatisticsService>(
    "daily-statistics", x => x.GenerateDailyStatistics(), "57 7 * * *");
RecurringJob.AddOrUpdate<IJobExpirationService>(
    "job-expiration", x => x.CheckAndExpireJobs(), "23 */2 * * *");
RecurringJob.AddOrUpdate<IResumeCleanupService>(
    "resume-cleanup", x => x.CleanupOldResumes(), "47 2 * * *");

// 种子默认工作流定义
try
{
    using var wfScope = app.Services.CreateScope();
    var db = wfScope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.SysConfigs.Any(c => c.ConfigKey == "WorkflowDefinitions_Seeded"))
    {
        var steps = System.Text.Json.JsonSerializer.Serialize(new object[]
        {
            new { StepName = "AI_Analyze", StepType = "ServiceAction", Config = new Dictionary<string, object?> { ["ServiceName"] = "IAIService", ["MethodName"] = "AnalyzeResumeAsync" }, NextSteps = new[] { "AI_Score" } },
            new { StepName = "AI_Score", StepType = "ServiceAction", Config = new Dictionary<string, object?> { ["ServiceName"] = "IAIService", ["MethodName"] = "ScoreResumeAsync" }, NextSteps = new[] { "Generate_Questions" } },
            new { StepName = "Generate_Questions", StepType = "ServiceAction", Config = new Dictionary<string, object?> { ["ServiceName"] = "IAIService", ["MethodName"] = "GenerateInterviewQuestionsAsync" }, NextSteps = new[] { "Notify_Complete" } },
            new { StepName = "Notify_Complete", StepType = "ServiceAction", Config = new Dictionary<string, object?> { ["ServiceName"] = "ISignalRService", ["MethodName"] = "SendToUserAsync" }, NextSteps = new string[] { } }
        });
        db.WorkflowDefinitions.Add(new WorkflowDefinition
        {
            Name = "RecruitmentPipeline",
            Description = "AI招聘全链路自动化",
            StepsJson = steps,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.SysConfigs.Add(new SysConfig { ConfigKey = "WorkflowDefinitions_Seeded", ConfigValue = "true", Description = "Workflow definition seeded" });
        db.SaveChanges();
    }
}
catch (Exception ex) { Console.WriteLine($"[Startup] 工作流种子跳过: {ex.Message}"); }

// ---- 中间件管道 ----
app.UseAppExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles(); // 允许访问 wwwroot（简历文件下载等）
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<NotificationHub>("/hubs/notification");
app.MapControllers();

app.Run();

public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.IsInRole("admin");
    }
}
