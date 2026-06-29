using Microsoft.AspNetCore.SignalR;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Services;

namespace Tripkitty.Api.Hubs;

public class SignalRTripNotifier(IHubContext<TripHub> hub) : ITripNotifier
{
    public Task TripUpdatedAsync(string tripId, TripDetailDto trip) =>
        hub.Clients.Group(tripId).SendAsync("trip:updated", trip);

    public Task TripDeletedAsync(string tripId) =>
        hub.Clients.Group(tripId).SendAsync("trip:deleted", new { tripId });

    public Task ExpenseAddedAsync(string tripId, ExpenseDto expense) =>
        hub.Clients.Group(tripId).SendAsync("expense:added", expense);

    public Task ExpenseRemovedAsync(string tripId, string expenseId) =>
        hub.Clients.Group(tripId).SendAsync("expense:removed", new { expenseId });

    public Task MemberAddedAsync(string tripId, GuestDto member) =>
        hub.Clients.Group(tripId).SendAsync("member:added", member);

    public Task ParticipantRemovedAsync(string tripId, string participantId) =>
        hub.Clients.Group(tripId).SendAsync("participant:removed", new { participantId });

    public Task EventAddedAsync(string tripId, TripEventDto ev) =>
        hub.Clients.Group(tripId).SendAsync("event:added", ev);

    public Task EventRemovedAsync(string tripId, string eventId) =>
        hub.Clients.Group(tripId).SendAsync("event:removed", new { eventId });
}
