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
        hub.Clients.Group(tripId).SendAsync("expense:added", new { tripId, expense });

    public Task ExpenseRemovedAsync(string tripId, string expenseId) =>
        hub.Clients.Group(tripId).SendAsync("expense:removed", new { tripId, expenseId });

    public Task MemberAddedAsync(string tripId, GuestDto member) =>
        hub.Clients.Group(tripId).SendAsync("member:added", new { tripId, id = member.Id, name = member.Name });

    public Task MemberInvitedAsync(string userId, string tripId) =>
        hub.Clients.User(userId).SendAsync("trip:joined", new { tripId });

    public Task ParticipantRemovedAsync(string tripId, string participantId) =>
        hub.Clients.Group(tripId).SendAsync("participant:removed", new { tripId, participantId });

    public Task EventAddedAsync(string tripId, TripEventDto ev) =>
        hub.Clients.Group(tripId).SendAsync("event:added", new { tripId, @event = ev });

    public Task EventRemovedAsync(string tripId, string eventId) =>
        hub.Clients.Group(tripId).SendAsync("event:removed", new { tripId, eventId });
}
