using System.Collections.Concurrent;
using System.Diagnostics;
using AIRecruitment.Api.Models;

namespace AIRecruitment.Api.Services;

public interface IHealthMonitorService
{
    Task<List<HealthCheckResult>> GetLatestResultsAsync();
    Task<HealthCheckResult> RunCheckAsync(string name, string url);
}

public record HealthCheckResult(string EndpointName, string Url, bool IsHealthy, long ResponseTimeMs, int StatusCode, DateTime CheckedAt);

public record HealthCheckEndpoint(string Name, string Url);

public class HealthCheckOptions
{
    public int IntervalSeconds { get; set; } = 30;
    public List<HealthCheckEndpoint> Endpoints { get; set; } = new();
}

public class HealthMonitorBackgroundService : BackgroundService, IHealthMonitorService
{
    private readonly ConcurrentDictionary<string, HealthCheckResult> _results = new();
    private readonly ConcurrentDictionary<string, bool> _previousHealth = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISignalRService _signalR;
    private readonly IConfiguration _config;
    private readonly ILogger<HealthMonitorBackgroundService> _logger;
    private readonly HttpClient _httpClient;

    public HealthMonitorBackgroundService(
        IServiceScopeFactory scopeFactory,
        ISignalRService signalR,
        IConfiguration config,
        ILogger<HealthMonitorBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _signalR = signalR;
        _config = config;
        _logger = logger;

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _config.GetSection("HealthChecks").Get<HealthCheckOptions>();
        var interval = options?.IntervalSeconds ?? 30;

        while (!stoppingToken.IsCancellationRequested)
        {
            var endpoints = options?.Endpoints ?? new List<HealthCheckEndpoint>();
            foreach (var ep in endpoints)
            {
                try { await RunCheckAsync(ep.Name, ep.Url); }
                catch (Exception ex) { _logger.LogWarning(ex, "Health check failed for {Endpoint}", ep.Name); }
            }
            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }
    }

    public async Task<HealthCheckResult> RunCheckAsync(string name, string url)
    {
        var sw = Stopwatch.StartNew();
        var healthy = false;
        var statusCode = 0;

        try
        {
            var resp = await _httpClient.GetAsync(url);
            statusCode = (int)resp.StatusCode;
            healthy = resp.IsSuccessStatusCode;
        }
        catch { healthy = false; }

        sw.Stop();
        var result = new HealthCheckResult(name, url, healthy, sw.ElapsedMilliseconds, statusCode, DateTime.UtcNow);
        _results[name] = result;

        var prevHealthy = _previousHealth.GetValueOrDefault(name, true);
        if (healthy != prevHealthy)
        {
            _previousHealth[name] = healthy;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SysOperLogs.Add(new SysOperLog
            {
                Module = "HealthMonitor",
                Action = healthy ? "Recovered" : "Failed",
                Detail = $"{name} ({url}) = {(healthy ? "UP" : "DOWN")}, {sw.ElapsedMilliseconds}ms",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            await _signalR.SendToAllAsync(NotificationEvents.MonitoringAlert, new
            {
                endpoint = name, url, healthy, responseTimeMs = sw.ElapsedMilliseconds
            });
        }

        return result;
    }

    public Task<List<HealthCheckResult>> GetLatestResultsAsync()
    {
        return Task.FromResult(_results.Values.ToList());
    }

    public override void Dispose()
    {
        _httpClient.Dispose();
        base.Dispose();
    }
}
