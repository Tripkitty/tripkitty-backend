namespace Tripkitty.Domain.Entities;

public class TripEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TripId { get; set; } = "";
    public Trip Trip { get; set; } = null!;
    public string Title { get; set; } = "";
    public DateOnly Date { get; set; }
    public TimeOnly? Time { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string CreatedBy { get; set; } = "";
}
