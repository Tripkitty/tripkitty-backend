using Microsoft.AspNetCore.SignalR;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Services;

namespace Tripkitty.Api.Hubs;

public class SignalRTripNotifier(IHubContext<TripHub> hub, ILogger<SignalRTripNotifier> logger) : ITripNotifier
{
    public Task TripUpdatedAsync(string tripId, TripDetailDto trip)
    {
        logger.LogInformation("SignalR send trip:updated to group {TripId}", tripId);
        return hub.Clients.Group(tripId).SendAsync("trip:updated", trip);
    }

    public Task TripDeletedAsync(string tripId)
    {
        logger.LogInformation("SignalR send trip:deleted to group {TripId}", tripId);
        return hub.Clients.Group(tripId).SendAsync("trip:deleted", new { tripId });
    }

    public Task ExpenseAddedAsync(string tripId, ExpenseDto expense)
    {
        logger.LogInformation("SignalR send expense:added to group {TripId} expense={ExpenseId}", tripId, expense.Id);
        return hub.Clients.Group(tripId).SendAsync("expense:added", new { tripId, expense });
    }

    public Task ExpenseUpdatedAsync(string tripId, ExpenseDto expense)
    {
        logger.LogInformation("SignalR send expense:updated to group {TripId} expense={ExpenseId}", tripId, expense.Id);
        return hub.Clients.Group(tripId).SendAsync("expense:updated", new { tripId, expense });
    }

    public Task ExpenseRemovedAsync(string tripId, string expenseId) =>
        hub.Clients.Group(tripId).SendAsync("expense:removed", new { tripId, expenseId });

    public Task MemberAddedAsync(string tripId, GuestDto member) =>
        hub.Clients.Group(tripId).SendAsync("member:added", new { tripId, id = member.Id, name = member.Name });

    public Task MemberInvitedAsync(string userId, string tripId) =>
        hub.Clients.User(userId).SendAsync("trip:joined", new { tripId });

    public Task ParticipantRemovedAsync(string tripId, string participantId) =>
        hub.Clients.Group(tripId).SendAsync("participant:removed", new { tripId, participantId });

    public Task EventAddedAsync(string tripId, TripEventDto ev)
    {
        logger.LogInformation("SignalR send event:added to group {TripId} event={EventId}", tripId, ev.Id);
        return hub.Clients.Group(tripId).SendAsync("event:added", new { tripId, @event = ev });
    }

    public Task EventUpdatedAsync(string tripId, TripEventDto ev)
    {
        logger.LogInformation("SignalR send event:updated to group {TripId} event={EventId}", tripId, ev.Id);
        return hub.Clients.Group(tripId).SendAsync("event:updated", new { tripId, @event = ev });
    }

    public Task EventRemovedAsync(string tripId, string eventId) =>
        hub.Clients.Group(tripId).SendAsync("event:removed", new { tripId, eventId });

    public Task SettlementUpdatedAsync(string tripId, SettlementsResponse settlements)
    {
        logger.LogInformation("SignalR send settlement:updated to group {TripId} status={Status}", tripId, settlements.Status);
        return hub.Clients.Group(tripId).SendAsync("settlement:updated", new { tripId, settlements });
    }
}
