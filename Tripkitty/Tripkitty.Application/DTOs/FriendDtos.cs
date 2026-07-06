using Tripkitty.Domain.Entities;

namespace Tripkitty.Application.DTOs;

public record SendFriendRequestRequest(string? Handle, string? UserId);

public record FriendDto(string Id, string Name, string LastName, string FirstName, string? MiddleName, string Handle, string Email)
{
    public static FriendDto From(User u) => new(u.Id, u.DisplayName, u.LastName, u.FirstName, u.MiddleName, u.Handle, u.Email);
}

public record FriendsResponse(
    List<FriendDto> Friends,
    List<FriendDto> Incoming,
    List<FriendDto> Outgoing
);
