namespace Tripkitty.Domain.Entities;

public class PushSubscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public User User { get; set; } = null!;
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
