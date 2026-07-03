using System.Security.Claims;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Services;

namespace Tripkitty.Api.Endpoints;

public static class FriendEndpoints
{
    public static IEndpointRouteBuilder MapFriendEndpoints(this IEndpointRouteBuilder app)
    {
        // User search
        app.MapGet("/users/search", async (string handle, ClaimsPrincipal user, IFriendService friendService) =>
        {
            var result = await friendService.SearchByHandleAsync(handle);
            return result is null ? Results.NotFound(new { error = new { code = "NOT_FOUND", message = "User not found" } })
                                  : Results.Ok(new { user = result });
        }).RequireAuthorization();

        var group = app.MapGroup("/me/friends").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IFriendService friendService) =>
        {
            var userId = GetUserId(user);
            var result = await friendService.GetFriendsAsync(userId);
            return Results.Ok(result);
        });

        group.MapPost("/requests", async (SendFriendRequestRequest request, ClaimsPrincipal user,
            IFriendService friendService) =>
        {
            var userId = GetUserId(user);
            await friendService.SendRequestAsync(userId, request);
            return Results.Ok(new { message = "Friend request sent" });
        });

        group.MapPost("/requests/{userId}/accept", async (string userId, ClaimsPrincipal user,
            IFriendService friendService) =>
        {
            var currentUserId = GetUserId(user);
            await friendService.AcceptAsync(currentUserId, userId);
            return Results.Ok(new { message = "Friend request accepted" });
        });

        group.MapPost("/requests/{userId}/decline", async (string userId, ClaimsPrincipal user,
            IFriendService friendService) =>
        {
            var currentUserId = GetUserId(user);
            await friendService.DeclineAsync(currentUserId, userId);
            return Results.Ok(new { message = "Friend request declined" });
        });

        group.MapDelete("/{userId}", async (string userId, ClaimsPrincipal user, IFriendService friendService) =>
        {
            var currentUserId = GetUserId(user);
            await friendService.RemoveFriendAsync(currentUserId, userId);
            return Results.Ok(new { message = "Friend removed" });
        });

        return app;
    }

    private static string GetUserId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("User ID not found in token");
}
