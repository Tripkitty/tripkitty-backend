using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Tripkitty.Api.Hubs;

[Authorize]
public class TripHub(ILogger<TripHub> logger) : Hub
{
    private string? UserId => Context.UserIdentifier;

    public override Task OnConnectedAsync()
    {
        logger.LogInformation("SignalR connected: conn={ConnectionId} user={UserId}",
            Context.ConnectionId, UserId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("SignalR disconnected: conn={ConnectionId} user={UserId} reason={Reason}",
            Context.ConnectionId, UserId, exception?.Message ?? "clean");
        return base.OnDisconnectedAsync(exception);
    }

    public async Task JoinTrip(string tripId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, tripId);
        logger.LogInformation("JoinTrip: conn={ConnectionId} user={UserId} trip={TripId}",
            Context.ConnectionId, UserId, tripId);
    }

    public async Task LeaveTrip(string tripId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tripId);
        logger.LogInformation("LeaveTrip: conn={ConnectionId} user={UserId} trip={TripId}",
            Context.ConnectionId, UserId, tripId);
    }
}
