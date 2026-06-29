namespace Tripkitty.Domain.Entities;

public class TripMember
{
    public string TripId { get; set; } = "";
    public string UserId { get; set; } = "";
    public Trip Trip { get; set; } = null!;
    public User User { get; set; } = null!;
}
