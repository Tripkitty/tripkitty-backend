using Tripkitty.Application.DTOs;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Services;

public interface IFriendService
{
    Task<UserDto?> SearchByHandleAsync(string handle);
    Task<FriendsResponse> GetFriendsAsync(string userId);
    Task SendRequestAsync(string currentUserId, SendFriendRequestRequest request);
    Task AcceptAsync(string currentUserId, string targetUserId);
    Task DeclineAsync(string currentUserId, string targetUserId);
    Task RemoveFriendAsync(string currentUserId, string targetUserId);
}

public class FriendService(
    IUserRepository userRepo,
    IFriendshipRepository friendRepo,
    IPushNotificationService pushService,
    IFriendNotifier friendNotifier) : IFriendService
{
    public async Task<UserDto?> SearchByHandleAsync(string handle)
    {
        var normalized = handle.TrimStart('@').ToLowerInvariant();
        var user = await userRepo.FindByHandleAsync(normalized);
        return user is null ? null : new UserDto(user.Id, user.Name, user.Handle, user.Email);
    }

    public async Task<FriendsResponse> GetFriendsAsync(string userId)
    {
        var friendships = await friendRepo.GetAllForUserAsync(userId);

        var friends = new List<FriendDto>();
        var incoming = new List<FriendDto>();
        var outgoing = new List<FriendDto>();

        foreach (var f in friendships)
        {
            if (f.Status == FriendshipStatus.Accepted)
            {
                var other = f.UserAId == userId ? f.UserB : f.UserA;
                friends.Add(new FriendDto(other.Id, other.Name, other.Handle, other.Email));
            }
            else if (f.Status == FriendshipStatus.Pending)
            {
                if (f.RequestedById == userId)
                {
                    var other = f.UserAId == userId ? f.UserB : f.UserA;
                    outgoing.Add(new FriendDto(other.Id, other.Name, other.Handle, other.Email));
                }
                else
                {
                    var other = f.UserAId == userId ? f.UserB : f.UserA;
                    incoming.Add(new FriendDto(other.Id, other.Name, other.Handle, other.Email));
                }
            }
        }

        return new FriendsResponse(friends, incoming, outgoing);
    }

    public async Task SendRequestAsync(string currentUserId, SendFriendRequestRequest request)
    {
        string? targetUserId = null;

        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            targetUserId = request.UserId;
        }
        else if (!string.IsNullOrWhiteSpace(request.Handle))
        {
            var handle = request.Handle.TrimStart('@').ToLowerInvariant();
            var user = await userRepo.FindByHandleAsync(handle)
                       ?? throw new DomainException("NOT_FOUND", "User not found");
            targetUserId = user.Id;
        }
        else
        {
            throw new DomainException("VALIDATION_ERROR", "Provide handle or userId");
        }

        if (targetUserId == currentUserId)
            throw new DomainException("SELF_REQUEST", "You cannot send a friend request to yourself");

        var (a, b) = Normalize(currentUserId, targetUserId);
        var existing = await friendRepo.FindAsync(a, b);

        if (existing is not null)
        {
            if (existing.Status == FriendshipStatus.Accepted)
                throw new DomainException("ALREADY_FRIENDS", "You are already friends");

            // Auto-accept if counter-request
            if (existing.RequestedById != currentUserId)
            {
                existing.Status = FriendshipStatus.Accepted;
                await friendRepo.SaveChangesAsync();

                var currentUser = await userRepo.FindByIdAsync(currentUserId);
                if (currentUser is not null)
                {
                    await pushService.NotifyAsync(targetUserId, "Запрос принят", $"{currentUser.Name} принял(а) ваш запрос в друзья");
                    await friendNotifier.FriendRequestAcceptedAsync(targetUserId,
                        new FriendDto(currentUser.Id, currentUser.Name, currentUser.Handle, currentUser.Email));
                }

                // Notify the current user too so their UI updates
                var counterUser = existing.UserAId == targetUserId ? existing.UserA : existing.UserB;
                if (counterUser is not null)
                {
                    await friendNotifier.FriendRequestAcceptedAsync(currentUserId,
                        new FriendDto(counterUser.Id, counterUser.Name, counterUser.Handle, counterUser.Email));
                }

                return;
            }

            throw new DomainException("REQUEST_EXISTS", "Friend request already sent");
        }

        var targetUser = await userRepo.FindByIdAsync(targetUserId)
                         ?? throw new DomainException("NOT_FOUND", "User not found");

        var senderUser = await userRepo.FindByIdAsync(currentUserId);

        var friendship = new Friendship
        {
            UserAId = a,
            UserBId = b,
            RequestedById = currentUserId,
            Status = FriendshipStatus.Pending
        };

        await friendRepo.AddAsync(friendship);
        await friendRepo.SaveChangesAsync();

        if (senderUser is not null)
            await pushService.NotifyAsync(targetUserId, "Запрос в друзья", $"{senderUser.Name} хочет добавить вас в друзья");
    }

    public async Task AcceptAsync(string currentUserId, string targetUserId)
    {
        var (a, b) = Normalize(currentUserId, targetUserId);
        var friendship = await friendRepo.FindAsync(a, b)
                         ?? throw new DomainException("NOT_FOUND", "Friend request not found");

        if (friendship.Status == FriendshipStatus.Accepted)
            throw new DomainException("ALREADY_FRIENDS", "Already friends");

        if (friendship.RequestedById == currentUserId)
            throw new DomainException("FORBIDDEN", "You cannot accept your own request");

        friendship.Status = FriendshipStatus.Accepted;
        await friendRepo.SaveChangesAsync();

        var acceptingUser = await userRepo.FindByIdAsync(currentUserId);
        if (acceptingUser is not null)
        {
            await pushService.NotifyAsync(friendship.RequestedById, "Запрос принят", $"{acceptingUser.Name} принял(а) ваш запрос в друзья");
            await friendNotifier.FriendRequestAcceptedAsync(friendship.RequestedById,
                new FriendDto(acceptingUser.Id, acceptingUser.Name, acceptingUser.Handle, acceptingUser.Email));
        }
    }

    public async Task DeclineAsync(string currentUserId, string targetUserId)
    {
        var (a, b) = Normalize(currentUserId, targetUserId);
        var friendship = await friendRepo.FindAsync(a, b)
                         ?? throw new DomainException("NOT_FOUND", "Friend request not found");

        if (friendship.Status == FriendshipStatus.Accepted)
            throw new DomainException("ALREADY_FRIENDS", "Cannot decline an accepted friendship");

        await friendRepo.RemoveAsync(friendship);
        await friendRepo.SaveChangesAsync();
    }

    public async Task RemoveFriendAsync(string currentUserId, string targetUserId)
    {
        var (a, b) = Normalize(currentUserId, targetUserId);
        var friendship = await friendRepo.FindAsync(a, b)
                         ?? throw new DomainException("NOT_FOUND", "Friendship not found");

        await friendRepo.RemoveAsync(friendship);
        await friendRepo.SaveChangesAsync();
    }

    private static (string, string) Normalize(string a, string b) =>
        string.Compare(a, b, StringComparison.Ordinal) < 0 ? (a, b) : (b, a);
}
