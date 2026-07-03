using Tripkitty.Domain.Entities;

namespace Tripkitty.Application.Services;

public interface IPushNotificationService
{
    Task NotifyAsync(string userId, string title, string body, string? url = null);
    Task NotifyManyAsync(IEnumerable<string> userIds, string title, string body, string? url = null);
}

public interface IPushSubscriptionRepository
{
    Task<List<PushSubscription>> GetByUserIdAsync(string userId);
    Task AddOrUpdateAsync(PushSubscription subscription);
    Task RemoveAsync(string userId, string endpoint);
    Task SaveChangesAsync();
}
