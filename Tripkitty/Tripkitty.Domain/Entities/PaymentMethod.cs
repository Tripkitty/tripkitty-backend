namespace Tripkitty.Domain.Entities;

// Способ оплаты в профиле пользователя: один номер телефона + список банков СБП,
// доступных по этому номеру. Пользователь может добавить несколько способов.
public class PaymentMethod
{
    public string Id { get; set; } = $"pm_{Guid.NewGuid():N}";
    public string UserId { get; set; } = "";
    public User User { get; set; } = null!;
    public string Phone { get; set; } = "";           // канонический формат +7XXXXXXXXXX
    public List<string> Banks { get; set; } = new();   // коды из BankCatalog, JSONB
    public string? Label { get; set; }
    public bool IsDefault { get; set; }
}
