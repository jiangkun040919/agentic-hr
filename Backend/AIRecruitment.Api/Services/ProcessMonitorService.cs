using System.Diagnostics;

namespace AIRecruitment.Api.Services;

public interface IProcessMonitorService
{
    Task<ProcessResult> RunAsync(string processPath, string arguments, int timeoutMs = 30000);
}

public record ProcessResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Duration);

public class ProcessMonitorService : IProcessMonitorService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISignalRService _signalR;

    public ProcessMonitorService(IServiceScopeFactory scopeFactory, ISignalRService signalR)
    {
        _scopeFactory = scopeFactory;
        _signalR = signalR;
    }

    public async Task<ProcessResult> RunAsync(string processPath, string arguments, int timeoutMs = 30000)
    {
        var sw = Stopwatch.StartNew();

        var psi = new ProcessStartInfo
        {
            FileName = processPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMs))
        {
            process.Kill(entireProcessTree: true);
            throw new Exception($"Process '{processPath}' timed out after {timeoutMs}ms");
        }

        var stdout = await outputTask;
        var stderr = await errorTask;
        sw.Stop();

        var result = new ProcessResult(process.ExitCode, stdout, stderr, sw.Elapsed);

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.SysOperLogs.Add(new Models.SysOperLog
                {
                    Module = "ProcessMonitor",
                    Action = $"{processPath} {arguments}",
                    Detail = $"ExitCode={result.ExitCode}, Duration={result.Duration.TotalSeconds:F1}s, Output={result.StandardOutput[..Math.Min(result.StandardOutput.Length, 500)]}",
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch { }
        });

        _ = _signalR.SendToAllAsync(NotificationEvents.ProcessCompleted, new
        {
            process = processPath,
            exitCode = result.ExitCode,
            duration = result.Duration.TotalSeconds
        });

        return result;
    }
}
