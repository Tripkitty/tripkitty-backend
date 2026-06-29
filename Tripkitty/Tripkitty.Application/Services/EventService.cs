using Tripkitty.Application.DTOs;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Services;

public interface IEventService
{
    Task<TripEventDto> AddAsync(string tripId, string userId, AddEventRequest request);
    Task RemoveAsync(string tripId, string userId, string eventId);
}

public class EventService(ITripRepository tripRepo) : IEventService
{
    public async Task<TripEventDto> AddAsync(string tripId, string userId, AddEventRequest request)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new DomainException("VALIDATION_ERROR", "Title is required", "title");

        if (!DateOnly.TryParse(request.Date, out var date))
            throw new DomainException("VALIDATION_ERROR", "Invalid date format (use yyyy-MM-dd)", "date");

        TimeOnly? time = null;
        if (!string.IsNullOrWhiteSpace(request.Time))
        {
            if (!TimeOnly.TryParse(request.Time, out var t))
                throw new DomainException("VALIDATION_ERROR", "Invalid time format", "time");
            time = t;
        }

        TimeOnly? endTime = null;
        if (!string.IsNullOrWhiteSpace(request.EndTime))
        {
            if (!TimeOnly.TryParse(request.EndTime, out var et))
                throw new DomainException("VALIDATION_ERROR", "Invalid end time format", "endTime");
            endTime = et;
        }

        var ev = new TripEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            TripId = tripId,
            Title = request.Title.Trim(),
            Date = date,
            Time = time,
            EndTime = endTime,
            CreatedBy = userId
        };

        trip.Events.Add(ev);
        trip.Version++;
        await tripRepo.SaveChangesAsync();

        return new TripEventDto(
            ev.Id, ev.Title,
            ev.Date.ToString("yyyy-MM-dd"),
            ev.Time?.ToString("HH:mm"),
            ev.EndTime?.ToString("HH:mm"),
            ev.CreatedBy
        );
    }

    public async Task RemoveAsync(string tripId, string userId, string eventId)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        var ev = trip.Events.FirstOrDefault(e => e.Id == eventId)
                 ?? throw new DomainException("NOT_FOUND", "Event not found");

        trip.Events.Remove(ev);
        trip.Version++;
        await tripRepo.SaveChangesAsync();
    }
}
