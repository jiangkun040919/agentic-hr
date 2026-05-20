using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace AIRecruitment.Api.Hubs;

public class NotificationHub : Hub
{
    private static readonly ConcurrentDictionary<int, string> _userConnections = new();

    public static string? GetConnectionId(int userId)
    {
        return _userConnections.TryGetValue(userId, out var connectionId) ? connectionId : null;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId > 0)
        {
            _userConnections[userId] = Context.ConnectionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId > 0)
        {
            _userConnections.TryRemove(userId, out _);
        }
        await base.OnDisconnectedAsync(exception);
    }

    private int GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    public async Task SubscribeToDelivery(int deliveryId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"delivery_{deliveryId}");
    }

    public async Task UnsubscribeFromDelivery(int deliveryId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"delivery_{deliveryId}");
    }
}