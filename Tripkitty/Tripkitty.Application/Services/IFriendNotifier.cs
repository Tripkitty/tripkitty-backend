using Tripkitty.Application.DTOs;

namespace Tripkitty.Application.Services;

public interface IFriendNotifier
{
    Task FriendRequestAcceptedAsync(string userId, FriendDto friend);
    Task FriendRequestReceivedAsync(string userId, FriendDto requester);
}
