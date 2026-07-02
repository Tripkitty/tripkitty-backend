using Microsoft.AspNetCore.SignalR;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Services;

namespace Tripkitty.Api.Hubs;

public class SignalRFriendNotifier(IHubContext<TripHub> hub) : IFriendNotifier
{
    public Task FriendRequestAcceptedAsync(string userId, FriendDto friend) =>
        hub.Clients.User(userId).SendAsync("friend:accepted", friend);

    public Task FriendRequestReceivedAsync(string userId, FriendDto requester) =>
        hub.Clients.User(userId).SendAsync("friend:request", requester);
}
