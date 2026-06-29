namespace Tripkitty.Application.DTOs;

public record SendFriendRequestRequest(string? Handle, string? UserId);

public record FriendDto(string Id, string Name, string Handle, string Email);

public record FriendsResponse(
    List<FriendDto> Friends,
    List<FriendDto> Incoming,
    List<FriendDto> Outgoing
);
