namespace Tripkitty.Domain.Entities;

public class User
{
    public string Id { get; set; } = $"u_{Guid.NewGuid():N}";
    public string Name { get; set; } = "";
    public string Handle { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public ICollection<Friendship> FriendshipsInitiated { get; set; } = new List<Friendship>();
    public ICollection<Friendship> FriendshipsReceived { get; set; } = new List<Friendship>();
    public ICollection<TripMember> TripMemberships { get; set; } = new List<TripMember>();
}
