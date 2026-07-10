using Microsoft.EntityFrameworkCore;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Infrastructure.Data;

public class AdminStatsRepository(AppDbContext db) : IAdminStatsRepository
{
    public Task<int> CountUsersAsync() => db.Users.CountAsync();

    public Task<int> CountUsersCreatedSinceAsync(DateTime since) =>
        db.Users.CountAsync(u => u.CreatedAt >= since);

    public Task<int> CountTripsByStatusAsync(int status) =>
        db.Trips.CountAsync(t => (int)t.Status == status);

    public Task<int> CountTripsAsync() => db.Trips.CountAsync();

    public Task<int> CountExpensesAsync() => db.Expenses.CountAsync();

    public Task<int> CountGuestsAsync() => db.Guests.CountAsync();

    public Task<int> CountFriendshipsAsync() => db.Friendships.CountAsync();
}
