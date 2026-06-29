using System.Security.Claims;
using Microsoft.Extensions.Options;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;
using Tripkitty.Infrastructure.Services;

namespace Tripkitty.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notifications").RequireAuthorization();

        group.MapGet("/vapid-public-key", (IOptions<WebPushOptions> opts) =>
            Results.Ok(new { publicKey = opts.Value.PublicKey }));

        group.MapPost("/subscribe", async (
            SavePushSubscriptionRequest request,
            ClaimsPrincipal user,
            IPushSubscriptionRepository repo) =>
        {
            var userId = GetUserId(user);
            var sub = new PushSubscription
            {
                UserId = userId,
                Endpoint = request.Endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth
            };
            await repo.AddOrUpdateAsync(sub);
            await repo.SaveChangesAsync();
            return Results.Ok();
        });

        group.MapDelete("/subscribe", async (
            RemovePushSubscriptionRequest request,
            ClaimsPrincipal user,
            IPushSubscriptionRepository repo) =>
        {
            var userId = GetUserId(user);
            await repo.RemoveAsync(userId, request.Endpoint);
            await repo.SaveChangesAsync();
            return Results.Ok();
        });

        return app;
    }

    private static string GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException();
}
