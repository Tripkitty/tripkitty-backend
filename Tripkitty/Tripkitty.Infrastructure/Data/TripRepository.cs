using Microsoft.EntityFrameworkCore;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Infrastructure.Data;

public class TripRepository(AppDbContext db) : ITripRepository
{
    public Task<List<Trip>> GetAllForUserAsync(string userId) =>
        db.Trips
            .Where(t => t.Members.Any(m => m.UserId == userId))
            .ToListAsync();

    public Task<Trip?> GetByIdWithDetailsAsync(string tripId) =>
        db.Trips
            .Include(t => t.Members).ThenInclude(m => m.User)
            .Include(t => t.Guests)
            .Include(t => t.Expenses)
            .Include(t => t.Events)
            .FirstOrDefaultAsync(t => t.Id == tripId);

    public Task<Trip?> GetByIdAsync(string tripId) =>
        db.Trips.FirstOrDefaultAsync(t => t.Id == tripId);

    public async Task AddAsync(Trip trip) => await db.Trips.AddAsync(trip);

    public Task DeleteAsync(Trip trip)
    {
        db.Trips.Remove(trip);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
