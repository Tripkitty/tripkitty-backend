using Microsoft.EntityFrameworkCore;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Infrastructure.Data;

public class FriendshipRepository(AppDbContext db) : IFriendshipRepository
{
    public Task<Friendship?> FindAsync(string userAId, string userBId) =>
        db.Friendships
            .Include(f => f.UserA)
            .Include(f => f.UserB)
            .Include(f => f.RequestedBy)
            .FirstOrDefaultAsync(f => f.UserAId == userAId && f.UserBId == userBId);

    public Task<List<Friendship>> GetAllForUserAsync(string userId) =>
        db.Friendships
            .Include(f => f.UserA)
            .Include(f => f.UserB)
            .Where(f => f.UserAId == userId || f.UserBId == userId)
            .ToListAsync();

    public async Task AddAsync(Friendship friendship) => await db.Friendships.AddAsync(friendship);

    public Task RemoveAsync(Friendship friendship)
    {
        db.Friendships.Remove(friendship);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
