namespace Tripkitty.Domain.Entities;

public class Expense
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TripId { get; set; } = "";
    public Trip Trip { get; set; } = null!;
    public string Title { get; set; } = "";
    public long AmountMinor { get; set; } // stored in minor units (e.g. kopeks)
    public string Payer { get; set; } = ""; // participantId
    public List<ShareEntry> Share { get; set; } = new();
    public SplitType SplitType { get; set; } = SplitType.Equal;
    public string CreatedBy { get; set; } = ""; // userId
}
