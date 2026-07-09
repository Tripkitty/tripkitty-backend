using Tripkitty.Application.DTOs;

namespace Tripkitty.Application.Services;

public interface ITripNotifier
{
    Task TripUpdatedAsync(string tripId, TripDetailDto trip);
    Task TripDeletedAsync(string tripId);
    Task ExpenseAddedAsync(string tripId, ExpenseDto expense);
    Task ExpenseUpdatedAsync(string tripId, ExpenseDto expense);
    Task ExpenseRemovedAsync(string tripId, string expenseId);
    Task MemberAddedAsync(string tripId, GuestDto member);
    Task MemberInvitedAsync(string userId, string tripId);
    Task ParticipantRemovedAsync(string tripId, string participantId);
    Task EventAddedAsync(string tripId, TripEventDto ev);
    Task EventUpdatedAsync(string tripId, TripEventDto ev);
    Task EventRemovedAsync(string tripId, string eventId);
    Task SettlementUpdatedAsync(string tripId, SettlementsResponse settlements);
}
