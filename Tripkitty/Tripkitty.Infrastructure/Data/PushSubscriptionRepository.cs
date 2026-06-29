using Microsoft.EntityFrameworkCore;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Infrastructure.Data;

public class PushSubscriptionRepository(AppDbContext db) : IPushSubscriptionRepository
{
    public async Task<List<PushSubscription>> GetByUserIdAsync(string userId) =>
        await db.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync();

    public async Task AddOrUpdateAsync(PushSubscription subscription)
    {
        var existing = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == subscription.UserId && s.Endpoint == subscription.Endpoint);

        if (existing is not null)
        {
            existing.P256dh = subscription.P256dh;
            existing.Auth = subscription.Auth;
        }
        else
        {
            db.PushSubscriptions.Add(subscription);
        }
    }

    public async Task RemoveAsync(string userId, string endpoint)
    {
        var sub = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint);
        if (sub is not null)
            db.PushSubscriptions.Remove(sub);
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
