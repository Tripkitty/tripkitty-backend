namespace Tripkitty.Domain.Entities;

public class Guest
{
    public string Id { get; set; } = $"g_{Guid.NewGuid():N}";
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string TripId { get; set; } = "";
    public Trip Trip { get; set; } = null!;
    public PaymentDetails? PaymentDetails { get; set; } // реквизиты гостя для перевода, JSONB
    public string? SponsorId { get; set; } // participantId того, кто платит за этого гостя (общий бюджет)

    // Не маппится в EF (нет сеттера) — отображаемое имя для DTO
    public string DisplayName => string.IsNullOrEmpty(LastName) ? FirstName : $"{FirstName} {LastName}";
}
