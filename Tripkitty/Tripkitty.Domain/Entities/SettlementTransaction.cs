namespace Tripkitty.Domain.Entities;

public class SettlementTransaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TripId { get; set; } = "";
    public Trip Trip { get; set; } = null!;
    public string FromId { get; set; } = ""; // participantId (u_* / g_*)
    public string ToId { get; set; } = "";
    public long AmountMinor { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaidMarkedById { get; set; } // userId, кто отметил оплату
}
