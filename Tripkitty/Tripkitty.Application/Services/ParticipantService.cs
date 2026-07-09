using Tripkitty.Application.DTOs;
using Tripkitty.Application.Logic;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Services;

public interface IParticipantService
{
    Task<GuestDto> AddMemberAsync(string tripId, string currentUserId, string targetUserId);
    Task<GuestDto> AddGuestAsync(string tripId, string currentUserId, AddGuestRequest request);
    Task<GuestDto> UpdateGuestAsync(string tripId, string currentUserId, string guestId, UpdateGuestRequest request);
    Task RemoveParticipantAsync(string tripId, string currentUserId, string participantId);
    Task<TripPaymentDto> GetMyPaymentAsync(string tripId, string userId);
    Task<TripPaymentDto> SetMyPaymentAsync(string tripId, string userId, PaymentDetailsRequest? request);
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
    IUserRepository userRepo,
    IPushNotificationService pushService,
    ITripNotifier notifier,
    IPaymentMethodRepository paymentMethodRepo) : IParticipantService
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

        EnsureActive(trip);

        // Must be a friend of current user
        var (a, b) = Normalize(currentUserId, targetUserId);
        var friendship = await friendRepo.FindAsync(a, b);
        if (friendship is null || friendship.Status != FriendshipStatus.Accepted)
            throw new DomainException("NOT_FRIENDS", "You can only add friends to a trip");

        var targetUser = await userRepo.FindByIdAsync(targetUserId)
                         ?? throw new DomainException("NOT_FOUND", "User not found");

        trip.Members.Add(new TripMember { TripId = tripId, UserId = targetUserId, User = targetUser });
        trip.Version++;
        await tripRepo.SaveChangesAsync();

        await pushService.NotifyAsync(targetUserId, "Вас добавили в поездку", trip.Name);

        await notifier.TripUpdatedAsync(tripId, TripService.MapToDetail(trip));
        await notifier.MemberInvitedAsync(targetUserId, tripId);
        return GuestDto.From(targetUser);
    }

    public async Task<GuestDto> AddGuestAsync(string tripId, string currentUserId, AddGuestRequest request)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == currentUserId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        EnsureActive(trip);

        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new DomainException("VALIDATION_ERROR", "Укажите фамилию гостя", "lastName");

        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new DomainException("VALIDATION_ERROR", "Укажите имя гостя", "firstName");

        var guest = new Guest
        {
            Id = $"g_{Guid.NewGuid():N}",
            LastName = request.LastName.Trim(),
            FirstName = request.FirstName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim(),
            TripId = tripId,
            PaymentDetails = PaymentDetailsFactory.FromRequest(request.PaymentDetails)
        };

        trip.Guests.Add(guest);
        trip.Version++;
        await tripRepo.SaveChangesAsync();

        await notifier.TripUpdatedAsync(tripId, TripService.MapToDetail(trip));
        return GuestDto.From(guest);
    }

    public async Task<GuestDto> UpdateGuestAsync(string tripId, string currentUserId, string guestId, UpdateGuestRequest request)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == currentUserId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        var guest = trip.Guests.FirstOrDefault(g => g.Id == guestId)
                    ?? throw new DomainException("GUEST_NOT_FOUND", "Гость не найден");

        if (request.LastName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.LastName))
                throw new DomainException("VALIDATION_ERROR", "Укажите фамилию гостя", "lastName");
            guest.LastName = request.LastName.Trim();
        }

        if (request.FirstName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName))
                throw new DomainException("VALIDATION_ERROR", "Укажите имя гостя", "firstName");
            guest.FirstName = request.FirstName.Trim();
        }

        if (request.MiddleName is not null)
            guest.MiddleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim();

        if (request.PaymentDetails is not null)
            guest.PaymentDetails = PaymentDetailsFactory.FromRequest(request.PaymentDetails);
        else if (request.ClearPayment)
            guest.PaymentDetails = null;

        trip.Version++;
        await tripRepo.SaveChangesAsync();

        await notifier.TripUpdatedAsync(tripId, TripService.MapToDetail(trip));
        return GuestDto.From(guest);
    }

    public async Task<TripPaymentDto> GetMyPaymentAsync(string tripId, string userId)
    {
        var member = await GetMemberAsync(tripId, userId);
        return await ResolvePaymentAsync(member);
    }

    public async Task<TripPaymentDto> SetMyPaymentAsync(string tripId, string userId, PaymentDetailsRequest? request)
    {
        var member = await GetMemberAsync(tripId, userId);

        // null → сброс override, реквизиты снова берутся из профиля.
        member.PaymentDetails = PaymentDetailsFactory.FromRequest(request);
        await tripRepo.SaveChangesAsync();

        return await ResolvePaymentAsync(member);
    }

    private async Task<TripMember> GetMemberAsync(string tripId, string userId)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        return trip.Members.FirstOrDefault(m => m.UserId == userId)
               ?? throw new DomainException("FORBIDDEN", "You are not a member of this trip");
    }

    private async Task<TripPaymentDto> ResolvePaymentAsync(TripMember member)
    {
        if (member.PaymentDetails is { } pd)
            return new TripPaymentDto(PaymentDetailsDto.From(pd), "trip");

        var methods = await paymentMethodRepo.GetForUserAsync(member.UserId);
        var fallback = methods.FirstOrDefault(m => m.IsDefault) ?? methods.FirstOrDefault();

        return fallback is null
            ? new TripPaymentDto(null, "none")
            : new TripPaymentDto(new PaymentDetailsDto(fallback.Phone, fallback.Banks, fallback.Label), "profile");
    }

    public async Task RemoveParticipantAsync(string tripId, string currentUserId, string participantId)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == currentUserId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        EnsureActive(trip);

        var memberToRemove = trip.Members.FirstOrDefault(m => m.UserId == participantId);
        var guestToRemove = trip.Guests.FirstOrDefault(g => g.Id == participantId);

        if (memberToRemove is null && guestToRemove is null)
            throw new DomainException("NOT_FOUND", "Participant not found in this trip");

        // Participant must have no expense involvement — payer or in someone's share —
        // before removal; caller deletes/reassigns those expenses first, no auto-cascade.
        var blockingExpenseIds = trip.Expenses
            .Where(e => e.Payer == participantId || e.Share.Any(s => s.ParticipantId == participantId))
            .Select(e => e.Id)
            .ToList();
        if (blockingExpenseIds.Count > 0)
            throw new DomainException("PARTICIPANT_HAS_EXPENSES",
                "Нельзя удалить участника, пока на нём есть расходы — сначала удалите или переназначьте их",
                details: new { expenseIds = blockingExpenseIds });

        if (memberToRemove is not null)
            trip.Members.Remove(memberToRemove);
        if (guestToRemove is not null)
            trip.Guests.Remove(guestToRemove);

        trip.Version++;
        await tripRepo.SaveChangesAsync();

        await notifier.TripUpdatedAsync(tripId, TripService.MapToDetail(trip));
    }

    private static void EnsureActive(Trip trip)
    {
        if (trip.Status != TripStatus.Active)
            throw new DomainException("TRIP_SETTLING",
                "Подсчёт завершён — изменения заблокированы. Переоткройте подсчёт, чтобы вносить правки");
    }

    private static (string, string) Normalize(string a, string b) =>
        string.Compare(a, b, StringComparison.Ordinal) < 0 ? (a, b) : (b, a);
}
