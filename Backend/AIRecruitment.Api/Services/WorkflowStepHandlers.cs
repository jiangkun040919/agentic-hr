using System.Text.Json;
using AIRecruitment.Api.Models.DTOs;

namespace AIRecruitment.Api.Services;

public record WorkflowStep(
    string StepName,
    string StepType,
    Dictionary<string, object?> Config,
    string[] NextSteps,
    int[]? ValidFromStatuses = null);

public record StepResult(bool Success, object? Data = null, string? Error = null, string? NextStep = null);

public interface IWorkflowStepHandler
{
    bool CanHandle(string stepType);
    Task<StepResult> ExecuteAsync(WorkflowStep step, object? input);
}

public class ProcessMonitorStepHandler : IWorkflowStepHandler
{
    private readonly IProcessMonitorService _processMonitor;

    public ProcessMonitorStepHandler(IProcessMonitorService processMonitor)
    {
        _processMonitor = processMonitor;
    }

    public bool CanHandle(string stepType) => stepType == "ProcessMonitor";

    public async Task<StepResult> ExecuteAsync(WorkflowStep step, object? input)
    {
        var path = step.Config.GetValueOrDefault("ProcessPath")?.ToString()
            ?? throw new Exception("ProcessPath not configured");
        var args = step.Config.GetValueOrDefault("Arguments")?.ToString() ?? "";
        var timeout = int.Parse(step.Config.GetValueOrDefault("TimeoutMs")?.ToString() ?? "30000");

        var result = await _processMonitor.RunAsync(path, args, timeout);
        return result.ExitCode == 0
            ? new StepResult(true, result)
            : new StepResult(false, result, $"Process exited with code {result.ExitCode}");
    }
}

public class FileWatchStepHandler : IWorkflowStepHandler
{
    private readonly IFileWatcherService _fileWatcher;

    public FileWatchStepHandler(IFileWatcherService fileWatcher)
    {
        _fileWatcher = fileWatcher;
    }

    public bool CanHandle(string stepType) => stepType == "FileWatch";

    public async Task<StepResult> ExecuteAsync(WorkflowStep step, object? input)
    {
        var dir = step.Config.GetValueOrDefault("WatchDirectory")?.ToString()
            ?? throw new Exception("WatchDirectory not configured");
        var pattern = step.Config.GetValueOrDefault("FilePattern")?.ToString() ?? "*.*";
        var timeout = int.Parse(step.Config.GetValueOrDefault("TimeoutSeconds")?.ToString() ?? "60");

        var filePath = await _fileWatcher.WaitForFileAsync(dir, pattern, timeout);
        return filePath != null
            ? new StepResult(true, new { FilePath = filePath })
            : new StepResult(false, null, $"File not detected in {timeout}s: {dir}\\{pattern}");
    }
}

public class ServiceActionStepHandler : IWorkflowStepHandler
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ServiceActionStepHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public bool CanHandle(string stepType) => stepType == "ServiceAction";

    public async Task<StepResult> ExecuteAsync(WorkflowStep step, object? input)
    {
        var serviceName = step.Config.GetValueOrDefault("ServiceName")?.ToString()
            ?? throw new Exception("ServiceName not configured");
        var methodName = step.Config.GetValueOrDefault("MethodName")?.ToString()
            ?? throw new Exception("MethodName not configured");

        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        object? result = null;

        switch (serviceName)
        {
            case "IAIService" when methodName == "AnalyzeResumeAsync":
                var aiSvc = provider.GetRequiredService<IAIService>();
                result = await aiSvc.AnalyzeResumeAsync(GetArg<int>(input, "DeliveryId"));
                break;

            case "IAIService" when methodName == "ScoreResumeAsync":
                var aiSvc2 = provider.GetRequiredService<IAIService>();
                result = await aiSvc2.ScoreResumeAsync(GetArg<int>(input, "DeliveryId"));
                break;

            case "IAIService" when methodName == "GenerateInterviewQuestionsAsync":
                var aiSvc3 = provider.GetRequiredService<IAIService>();
                result = await aiSvc3.GenerateInterviewQuestionsAsync(GetArg<int>(input, "DeliveryId"));
                break;

            case "IInterviewService" when methodName == "ScheduleInterviewAsync":
                var ivSvc = provider.GetRequiredService<IInterviewService>();
                var formData = JsonSerializer.Deserialize<InterviewFormData>(
                    JsonSerializer.Serialize(input ?? new { }),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                result = await ivSvc.ScheduleInterviewAsync(formData!);
                break;

            case "IDeliveryService" when methodName == "UpdateDeliveryStatusAsync":
                var dlvSvc = provider.GetRequiredService<IDeliveryService>();
                await dlvSvc.UpdateDeliveryStatusAsync(
                    GetArg<int>(input, "DeliveryId"),
                    GetArg<int>(input, "Status"),
                    GetArg<string?>(input, "Remark"));
                result = "ok";
                break;

            case "ISignalRService" when methodName == "SendToUserAsync":
                var sigSvc = provider.GetRequiredService<ISignalRService>();
                await sigSvc.SendToUserAsync(
                    GetArg<int>(input, "UserId"),
                    GetArg<string>(input, "Event"),
                    input ?? new { });
                result = "ok";
                break;

            default:
                throw new Exception($"Unknown service action: {serviceName}.{methodName}");
        }

        return new StepResult(true, result);
    }

    private static T GetArg<T>(object? input, string key)
    {
        if (input is JsonElement el && el.TryGetProperty(key, out var prop))
            return JsonSerializer.Deserialize<T>(prop.GetRawText())!;
        if (input is IDictionary<string, object?> dict && dict.TryGetValue(key, out var val))
            return (T)val!;
        throw new Exception($"Missing input argument: {key}");
    }
}

public class ConditionStepHandler : IWorkflowStepHandler
{
    public bool CanHandle(string stepType) => stepType == "Condition";

    public Task<StepResult> ExecuteAsync(WorkflowStep step, object? input)
    {
        var field = step.Config.GetValueOrDefault("Field")?.ToString() ?? "";
        var op = step.Config.GetValueOrDefault("Operator")?.ToString() ?? "eq";
        var expected = step.Config.GetValueOrDefault("Value");
        var trueBranch = step.Config.GetValueOrDefault("TrueBranch")?.ToString();
        var falseBranch = step.Config.GetValueOrDefault("FalseBranch")?.ToString();

        var actual = GetValue(input, field);
        var match = op switch
        {
            "eq" => Equals(actual, expected),
            "neq" => !Equals(actual, expected),
            "gt" => CompareAs<double>(actual, expected) > 0,
            "lt" => CompareAs<double>(actual, expected) < 0,
            "gte" => CompareAs<double>(actual, expected) >= 0,
            "lte" => CompareAs<double>(actual, expected) <= 0,
            "contains" => actual?.ToString()?.Contains(expected?.ToString() ?? "") == true,
            _ => false
        };

        return Task.FromResult(new StepResult(true,
            new { ConditionMet = match, Branch = match ? (trueBranch ?? "true") : (falseBranch ?? "false") },
            NextStep: match ? trueBranch : falseBranch));
    }

    private static object? GetValue(object? input, string field)
    {
        if (input is JsonElement el && el.TryGetProperty(field, out var prop))
            return prop.ToString();
        if (input is IDictionary<string, object?> dict && dict.TryGetValue(field, out var val))
            return val;
        return null;
    }

    private static int CompareAs<T>(object? a, object? b) where T : IComparable<T>
    {
        var va = (T)Convert.ChangeType(a, typeof(T));
        var vb = (T)Convert.ChangeType(b, typeof(T));
        return va.CompareTo(vb);
    }
}
