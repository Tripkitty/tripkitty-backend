namespace Tripkitty.Application.DTOs;

public record SavePushSubscriptionRequest(string Endpoint, string P256dh, string Auth);

public record RemovePushSubscriptionRequest(string Endpoint);
