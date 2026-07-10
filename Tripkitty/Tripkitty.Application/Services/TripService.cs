using Tripkitty.Application.DTOs;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Services;

public interface ITripService
{
    Task<List<TripSummaryDto>> GetAllAsync(string userId);
    Task<TripDetailDto> GetByIdAsync(string tripId, string userId);
    Task<TripDetailDto> CreateAsync(CreateTripRequest request, string userId);
    Task<TripDetailDto> PatchAsync(string tripId, string userId, PatchTripRequest request, long expectedVersion);
    Task ClearAsync(string tripId, string userId);
    Task DeleteAsync(string tripId, string userId);
}

public interface ITripRepository
{
    Task<List<Trip>> GetAllForUserAsync(string userId);
    Task<Trip?> GetByIdWithDetailsAsync(string tripId);
    Task<Trip?> GetByIdAsync(string tripId);
    Task<Trip?> GetByCalendarTokenAsync(string calendarToken);
    Task AddAsync(Trip trip);
    Task DeleteAsync(Trip trip);
    Task SaveChangesAsync();
}

public class TripService(
    ITripRepository tripRepo,
    ITripNotifier notifier) : ITripService
{
    public async Task<List<TripSummaryDto>> GetAllAsync(string userId)
    {
        var trips = await tripRepo.GetAllForUserAsync(userId);
        return trips.Select(MapToSummary).ToList();
    }

    public async Task<TripDetailDto> GetByIdAsync(string tripId, string userId)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        return MapToDetail(trip);
    }

    public async Task<TripDetailDto> CreateAsync(CreateTripRequest request, string userId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("VALIDATION_ERROR", "Trip name is required", "name");

        var trip = new Trip
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = request.Name.Trim(),
            Cur = string.IsNullOrWhiteSpace(request.Cur) ? "RUB" : request.Cur.ToUpperInvariant(),
            OwnerId = userId,
            Version = 1
        };

        trip.Members.Add(new TripMember { TripId = trip.Id, UserId = userId });

        await tripRepo.AddAsync(trip);
        await tripRepo.SaveChangesAsync();

        var created = await tripRepo.GetByIdWithDetailsAsync(trip.Id)
                      ?? throw new DomainException("NOT_FOUND", "Trip not found after creation");

        return MapToDetail(created);
    }

    public async Task<TripDetailDto> PatchAsync(string tripId, string userId, PatchTripRequest request, long expectedVersion)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        if (trip.Version != expectedVersion)
            throw new DomainException("VERSION_CONFLICT", "Trip has been modified by another request");

        if (request.Name is not null)
            trip.Name = request.Name.Trim();
        if (request.Start.HasValue)
            trip.Start = request.Start;
        if (request.End.HasValue)
            trip.End = request.End;

        trip.Version++;
        await tripRepo.SaveChangesAsync();

        var dto = MapToDetail(trip);
        _ = notifier.TripUpdatedAsync(tripId, dto);
        return dto;
    }

    public async Task ClearAsync(string tripId, string userId)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        trip.Expenses.Clear();
        trip.Guests.Clear();
        trip.Settlements.Clear();
        trip.Status = TripStatus.Active;
        trip.Version++;
        await tripRepo.SaveChangesAsync();

        var dto = MapToDetail(trip);
        _ = notifier.TripUpdatedAsync(tripId, dto);
    }

    public async Task DeleteAsync(string tripId, string userId)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        if (trip.OwnerId != userId)
            throw new DomainException("FORBIDDEN", "Only the owner can delete a trip");

        await tripRepo.DeleteAsync(trip);
        await tripRepo.SaveChangesAsync();

        _ = notifier.TripDeletedAsync(tripId);
    }

    private static TripSummaryDto MapToSummary(Trip t) =>
        new(t.Id, t.Name, t.Cur, t.OwnerId, t.Start, t.End, t.Version, t.Status.ToDto());

    public static TripDetailDto MapToDetail(Trip t) =>
        new(
            t.Id, t.Name, t.Cur, t.OwnerId, t.Start, t.End, t.Version, t.Status.ToDto(),
            t.Members.Select(MemberDto.From).ToList(),
            t.Guests.Select(g => GuestDto.From(g)).ToList(),
            t.Expenses.Select(e => new ExpenseDto(
                e.Id, e.Title, e.AmountMinor / 100m, e.Payer,
                e.Share.Select(s => new ShareEntryDto(
                    s.ParticipantId, s.Weight,
                    s.AmountMinor.HasValue ? s.AmountMinor.Value / 100m : null
                )).ToList(),
                e.SplitType, e.CreatedBy, e.IsTransfer
            )).ToList(),
            t.Events.Select(ev => new TripEventDto(
                ev.Id, ev.Title,
                ev.Date.ToString("yyyy-MM-dd"),
                ev.Time?.ToString("HH:mm"),
                ev.EndTime?.ToString("HH:mm"),
                ev.CreatedBy
            )).ToList()
        );
}
