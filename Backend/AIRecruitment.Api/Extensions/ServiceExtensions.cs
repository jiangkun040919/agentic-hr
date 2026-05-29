using System.Text;
using AIRecruitment.Api.Hubs;
using AIRecruitment.Api.Options;
using AIRecruitment.Api.Services;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

namespace AIRecruitment.Api.Extensions;

internal static class ServiceExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<AIOptions>(configuration.GetSection(AIOptions.Section));
        services.Configure<MinIOOptions>(configuration.GetSection(MinIOOptions.Section));
        services.Configure<TencentCloudOptions>(configuration.GetSection(TencentCloudOptions.Section));

        var jwtOptions = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()!;

        AddJwtAuth(services, jwtOptions);
        AddDatabase(services, configuration);
        AddRedis(services, configuration);
        AddSwagger(services);
        AddCors(services);
        AddAppDependencies(services, configuration);

        return services;
    }

    private static void AddJwtAuth(IServiceCollection services, JwtOptions jwtOptions)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Issuer,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connStr));
    }

    private static void AddRedis(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConfig = ConfigurationOptions.Parse(
                configuration.GetConnectionString("Redis") ?? "localhost:6379");
            return ConnectionMultiplexer.Connect(redisConfig);
        });
    }

    private static void AddSwagger(IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "AI智能招聘管理系统 API", Version = "v1" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
    }

    private static void AddCors(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            });
        });
    }

    private static void AddAppDependencies(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSignalR();
        services.AddHttpClient();
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy =
                    System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.ReferenceHandler =
                    System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            });

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IDeliveryService, DeliveryService>();
        services.AddScoped<IAIService, AIService>();
        services.AddScoped<IInterviewService, InterviewService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
            services.AddScoped<TemplateDrivenCollector>();
        services.AddScoped<TemplateGenerationService>();
        services.AddScoped<IAIInterviewService, AIInterviewService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<KnowledgeGraphService>();
        services.AddSingleton<DataCollectionService>();
        services.AddSingleton<DataSeederService>();
        services.AddSingleton<ISignalRService, SignalRService>();
        services.AddScoped<ScheduledTasksService>();

        // Hangfire 后台任务
        services.AddScoped<IDailyStatisticsService, DailyStatisticsService>();
        services.AddScoped<IJobExpirationService, JobExpirationService>();
        services.AddScoped<IResumeCleanupService, ResumeCleanupService>();

        // RabbitMQ（可选，通过配置开关控制）
        var rabbitEnabled = configuration.GetValue<bool>("RabbitMQ:Enabled");
        if (rabbitEnabled)
        {
            services.AddSingleton<IRabbitMQService, RabbitMQService>();
            services.AddHostedService<RabbitMQConsumerService>();
        }
        else
        {
            services.AddSingleton<IRabbitMQService, RabbitMQService>(); // 仍注册但不会真连
        }

        // 工作流引擎
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<IWorkflowStepHandler, ProcessMonitorStepHandler>();
        services.AddScoped<IWorkflowStepHandler, FileWatchStepHandler>();
        services.AddScoped<IWorkflowStepHandler, ServiceActionStepHandler>();
        services.AddScoped<IWorkflowStepHandler, ConditionStepHandler>();

        // PDF解析
        services.AddSingleton<IPdfExtractService, PdfExtractService>();

        // 进程与文件监控
        services.AddSingleton<IProcessMonitorService, ProcessMonitorService>();
        services.AddSingleton<IFileWatcherService, FileWatcherService>();

        // 健康监控
        services.Configure<HealthCheckOptions>(configuration.GetSection("HealthChecks"));
        services.AddSingleton<IHealthMonitorService, HealthMonitorBackgroundService>();
        services.AddHostedService<HealthMonitorBackgroundService>();

        // 准确率评测
        services.AddScoped<IAccuracyTestService, AccuracyTestService>();

        // AI简历解析/匹配/面试建议
        services.AddScoped<IResumeAiService, ResumeAiService>();

        // 赛事增强功能
        services.AddScoped<EnhancedMatchingService>();
        services.AddScoped<JobDiscoveryService>();

        // ═══ V2 增强：ML + 多智能体 + Graph RAG ═══
        services.AddSingleton<MLMatchingService>();
        services.AddScoped<MultiAgentMatchingService>();
        services.AddScoped<GraphRAGService>();

        // 动态演化演示
        services.AddScoped<EvolutionDemoService>();

        // 图谱自演化引擎（真实数据驱动）
        services.AddScoped<GraphEvolutionService>();

        // 招聘决策智能引擎（可解释匹配 + 风险雷达 + WhatIf）
        services.AddScoped<DecisionIntelligenceService>();

        // 候选人端智能服务（成长路径 + 竞争力 + 透明匹配）
        services.AddScoped<CandidateIntelligenceService>();

        // 比赛专项：数据交叉验证 + 准确率评测
        services.AddScoped<DataCrossValidationService>();
        services.AddScoped<BenchmarkDataService>();

        // 实时岗位采集
        services.AddScoped<RealtimeJobCollectorService>();

        // AI 公平性审计
        services.AddScoped<FairnessAuditService>();

        // Agentic AI 招聘专员
        services.AddScoped<RecruitmentAgentService>();

        // Excel 导出
        services.AddScoped<ExportService>();
    }

    public static IServiceCollection AddAppHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("DefaultConnection");
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connStr));
        services.AddHangfireServer();
        return services;
    }
}
