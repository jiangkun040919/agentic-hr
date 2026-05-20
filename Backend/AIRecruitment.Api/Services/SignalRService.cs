using Microsoft.AspNetCore.SignalR;
using AIRecruitment.Api.Hubs;

namespace AIRecruitment.Api.Services;

public interface ISignalRService
{
    Task SendToUserAsync(int userId, string method, object data);
    Task SendToAllAsync(string method, object data);
    Task SendToGroupAsync(string groupName, string method, object data);
}

public static class NotificationEvents
{
    public const string AIProcessingComplete = "AIProcessingComplete";
    public const string InterviewScheduled = "InterviewScheduled";
    public const string WorkflowStepCompleted = "WorkflowStepCompleted";
    public const string ProcessCompleted = "ProcessCompleted";
    public const string FileDetected = "FileDetected";
    public const string MonitoringAlert = "MonitoringAlert";
    public const string HealthStatusChanged = "HealthStatusChanged";
}

public class SignalRService : ISignalRService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendToUserAsync(int userId, string method, object data)
    {
        var connectionId = NotificationHub.GetConnectionId(userId);
        if (connectionId != null)
        {
            await _hubContext.Clients.Client(connectionId).SendAsync(method, data);
        }
        else
        {
            await _hubContext.Clients.Group($"user_{userId}").SendAsync(method, data);
        }
    }

    public async Task SendToAllAsync(string method, object data)
    {
        await _hubContext.Clients.All.SendAsync(method, data);
    }

    public async Task SendToGroupAsync(string groupName, string method, object data)
    {
        await _hubContext.Clients.Group(groupName).SendAsync(method, data);
    }
}