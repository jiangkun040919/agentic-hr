using AIRecruitment.Api.Data;
using AIRecruitment.Api.Extensions;
using AIRecruitment.Api.Hubs;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Services;
using Hangfire;
using Hangfire.Dashboard;

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
        db.Database.EnsureCreated();
        Console.WriteLine("[Startup] EF Core 数据库表初始化完成");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] EF Core 建表失败（不影响启动）: {ex.Message}");
    }
}
DbInitializer.EnsureTables(builder.Configuration);

// ---- 播种演示数据 ----
try
{
    using var seedScope = app.Services.CreateScope();
    var seeder = seedScope.ServiceProvider.GetRequiredService<DataSeederService>();
    await seeder.SeedAsync();
    Console.WriteLine("[Startup] 演示数据初始化完成");
}
catch (Exception ex) { Console.WriteLine($"[Startup] 数据播种跳过: {ex.Message}"); }

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
RecurringJob.AddOrUpdate<ScheduledTasksService>(
    "graph-snapshot-weekly", x => x.TakeGraphSnapshotAsync(), "0 2 * * 1");

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
            CreatedAt = DateTime.Now
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
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<NotificationHub>("/hubs/notification");
app.MapControllers();

app.Run();

public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
