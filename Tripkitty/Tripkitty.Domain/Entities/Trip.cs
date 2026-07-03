namespace Tripkitty.Domain.Entities;

public class Trip
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Cur { get; set; } = "RUB";
    public string OwnerId { get; set; } = "";
    public User Owner { get; set; } = null!;
    public DateOnly? Start { get; set; }
    public DateOnly? End { get; set; }
    public long Version { get; set; } = 1;
    public ICollection<TripMember> Members { get; set; } = new List<TripMember>();
    public ICollection<Guest> Guests { get; set; } = new List<Guest>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<TripEvent> Events { get; set; } = new List<TripEvent>();
}
