namespace Tripkitty.Domain.Entities;

public class TripMember
{
    public string TripId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string CalendarToken { get; set; } = Guid.NewGuid().ToString("N");
    public Trip Trip { get; set; } = null!;
    public User User { get; set; } = null!;
    public PaymentDetails? PaymentDetails { get; set; } // реквизиты юзера в этой поездке (override), JSONB
    public string? SponsorId { get; set; } // participantId того, кто платит за этого участника (общий бюджет)
}
