namespace Tripkitty.Domain.Entities;

// Реквизиты получателя для перевода: один номер + список банков СБП.
// Хранится как JSONB на TripMember (реквизиты юзера в поездке) и Guest (реквизиты гостя).
public class PaymentDetails
{
    public string Phone { get; set; } = "";        // канонический формат +7XXXXXXXXXX
    public List<string> Banks { get; set; } = new(); // коды из BankCatalog
    public string? Label { get; set; }
}
