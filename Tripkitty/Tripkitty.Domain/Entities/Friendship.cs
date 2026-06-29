namespace Tripkitty.Domain.Entities;

public class Friendship
{
    public string RequestedById { get; set; } = "";
    public string UserAId { get; set; } = ""; // always < UserBId alphabetically
    public string UserBId { get; set; } = "";
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public User UserA { get; set; } = null!;
    public User UserB { get; set; } = null!;
    public User RequestedBy { get; set; } = null!;
}

public enum FriendshipStatus { Pending, Accepted }
