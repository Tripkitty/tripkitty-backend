using Tripkitty.Application.DTOs;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Services;

public interface IParticipantService
{
    Task<GuestDto> AddMemberAsync(string tripId, string currentUserId, string targetUserId);
    Task<GuestDto> AddGuestAsync(string tripId, string currentUserId, string guestName);
    Task RemoveParticipantAsync(string tripId, string currentUserId, string participantId);
}

public interface IFriendshipRepository
{
    Task<Friendship?> FindAsync(string userAId, string userBId);
    Task<List<Friendship>> GetAllForUserAsync(string userId);
    Task AddAsync(Friendship friendship);
    Task RemoveAsync(Friendship friendship);
    Task SaveChangesAsync();
}

public class ParticipantService(
    ITripRepository tripRepo,
    IFriendshipRepository friendRepo,
    IUserRepository userRepo) : IParticipantService
{
    public async Task<GuestDto> AddMemberAsync(string tripId, string currentUserId, string targetUserId)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var currentIsMember = trip.Members.Any(m => m.UserId == currentUserId);
        if (!currentIsMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        if (trip.Members.Any(m => m.UserId == targetUserId))
            throw new DomainException("ALREADY_MEMBER", "User is already a member of this trip");

        // Must be a friend of current user
        var (a, b) = Normalize(currentUserId, targetUserId);
        var friendship = await friendRepo.FindAsync(a, b);
        if (friendship is null || friendship.Status != FriendshipStatus.Accepted)
            throw new DomainException("NOT_FRIENDS", "You can only add friends to a trip");

        var targetUser = await userRepo.FindByIdAsync(targetUserId)
                         ?? throw new DomainException("NOT_FOUND", "User not found");

        trip.Members.Add(new TripMember { TripId = tripId, UserId = targetUserId });
        trip.Version++;
        await tripRepo.SaveChangesAsync();

        return new GuestDto(targetUser.Id, targetUser.Name);
    }

    public async Task<GuestDto> AddGuestAsync(string tripId, string currentUserId, string guestName)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == currentUserId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        if (string.IsNullOrWhiteSpace(guestName))
            throw new DomainException("VALIDATION_ERROR", "Guest name is required", "name");

        var guest = new Guest
        {
            Id = $"g_{Guid.NewGuid():N}",
            Name = guestName.Trim(),
            TripId = tripId
        };

        trip.Guests.Add(guest);
        trip.Version++;
        await tripRepo.SaveChangesAsync();

        return new GuestDto(guest.Id, guest.Name);
    }

    public async Task RemoveParticipantAsync(string tripId, string currentUserId, string participantId)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == currentUserId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        // Atomic cascade removal
        // 1. Remove from members or guests
        var memberToRemove = trip.Members.FirstOrDefault(m => m.UserId == participantId);
        var guestToRemove = trip.Guests.FirstOrDefault(g => g.Id == participantId);

        if (memberToRemove is null && guestToRemove is null)
            throw new DomainException("NOT_FOUND", "Participant not found in this trip");

        if (memberToRemove is not null)
            trip.Members.Remove(memberToRemove);
        if (guestToRemove is not null)
            trip.Guests.Remove(guestToRemove);

        // 2. Delete expenses where payer == participantId
        var expensesToRemove = trip.Expenses.Where(e => e.Payer == participantId).ToList();
        foreach (var e in expensesToRemove)
            trip.Expenses.Remove(e);

        // 3. Remove participantId from all share arrays
        foreach (var expense in trip.Expenses)
        {
            expense.Share = expense.Share.Where(s => s != participantId).ToList();
        }

        // 4. Delete expenses where share is now empty
        var emptyShareExpenses = trip.Expenses.Where(e => e.Share.Count == 0).ToList();
        foreach (var e in emptyShareExpenses)
            trip.Expenses.Remove(e);

        trip.Version++;
        await tripRepo.SaveChangesAsync();
    }

    private static (string, string) Normalize(string a, string b) =>
        string.Compare(a, b, StringComparison.Ordinal) < 0 ? (a, b) : (b, a);
}
