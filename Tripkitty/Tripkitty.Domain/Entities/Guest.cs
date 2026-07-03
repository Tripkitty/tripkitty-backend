namespace Tripkitty.Domain.Entities;

public class Guest
{
    public string Id { get; set; } = $"g_{Guid.NewGuid():N}";
    public string Name { get; set; } = "";
    public string TripId { get; set; } = "";
    public Trip Trip { get; set; } = null!;
}
