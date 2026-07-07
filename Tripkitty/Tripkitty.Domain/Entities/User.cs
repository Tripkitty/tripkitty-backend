namespace Tripkitty.Domain.Entities;

public class User
{
    public string Id { get; set; } = $"u_{Guid.NewGuid():N}";
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string Handle { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    // Не маппится в EF (нет сеттера) — отображаемое имя для пушей и DTO
    public string DisplayName => string.IsNullOrEmpty(LastName) ? FirstName : $"{FirstName} {LastName}";

    public ICollection<Friendship> FriendshipsInitiated { get; set; } = new List<Friendship>();
    public ICollection<Friendship> FriendshipsReceived { get; set; } = new List<Friendship>();
    public ICollection<TripMember> TripMemberships { get; set; } = new List<TripMember>();
    public ICollection<PaymentMethod> PaymentMethods { get; set; } = new List<PaymentMethod>();
}
