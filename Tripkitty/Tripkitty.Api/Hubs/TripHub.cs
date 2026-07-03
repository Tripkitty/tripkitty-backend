using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Tripkitty.Api.Hubs;

[Authorize]
public class TripHub : Hub
{
    public async Task JoinTrip(string tripId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, tripId);

    public async Task LeaveTrip(string tripId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tripId);
}
