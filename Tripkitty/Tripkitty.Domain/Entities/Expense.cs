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
    public bool IsTransfer { get; set; } // перевод, созданный при reopen из оплаченной транзакции; read-only
    public long? GrossAmountMinor { get; set; } // сумма до скидки, если скидка была
    public decimal? DiscountPercent { get; set; } // 0..100, если скидка процентом
    public long? DiscountAmountMinor { get; set; } // фикс. скидка в минорных единицах

}
