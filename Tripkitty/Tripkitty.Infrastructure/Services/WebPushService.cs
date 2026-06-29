using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tripkitty.Application.Services;
using WebPush;

namespace Tripkitty.Infrastructure.Services;

public class WebPushOptions
{
    public string Subject { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
}

public class WebPushService(
    IPushSubscriptionRepository subscriptionRepo,
    IOptions<WebPushOptions> options,
    ILogger<WebPushService> logger) : IPushNotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task NotifyAsync(string userId, string title, string body, string? url = null)
    {
        var subscriptions = await subscriptionRepo.GetByUserIdAsync(userId);
        if (subscriptions.Count == 0) return;

        var payload = JsonSerializer.Serialize(new { title, body, url }, JsonOptions);
        var vapid = options.Value;

        foreach (var sub in subscriptions)
        {
            try
            {
                var client = new WebPushClient();
                client.SetVapidDetails(vapid.Subject, vapid.PublicKey, vapid.PrivateKey);
                var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSub, payload);
            }
            catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone)
            {
                await subscriptionRepo.RemoveAsync(userId, sub.Endpoint);
                await subscriptionRepo.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send push notification to user {UserId}", userId);
            }
        }
    }

    public async Task NotifyManyAsync(IEnumerable<string> userIds, string title, string body, string? url = null)
    {
        await Task.WhenAll(userIds.Select(uid => NotifyAsync(uid, title, body, url)));
    }
}
