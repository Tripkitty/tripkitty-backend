namespace Tripkitty.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
}
