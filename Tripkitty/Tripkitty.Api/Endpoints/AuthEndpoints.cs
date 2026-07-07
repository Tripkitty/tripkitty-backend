using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Services;

namespace Tripkitty.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", async (RegisterRequest request, IAuthService auth) =>
        {
            var result = await auth.RegisterAsync(request);
            return Results.Ok(result);
        });

        group.MapPost("/login", async (LoginRequest request, IAuthService auth) =>
        {
            var result = await auth.LoginAsync(request);
            return Results.Ok(result);
        });

        group.MapPost("/refresh", async (RefreshRequest request, IAuthService auth) =>
        {
            var result = await auth.RefreshAsync(request.RefreshToken);
            return Results.Ok(result);
        });

        group.MapPost("/logout", async (RefreshRequest request, IAuthService auth) =>
        {
            await auth.LogoutAsync(request.RefreshToken);
            return Results.Ok(new { message = "Logged out" });
        });

        group.MapGet("/me", async (ClaimsPrincipal user, IAuthService auth) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? user.FindFirst("sub")?.Value
                         ?? throw new UnauthorizedAccessException();
            var me = await auth.GetMeAsync(userId);
            return Results.Ok(new { user = me });
        }).RequireAuthorization();

        group.MapPatch("/me", async (UpdateProfileRequest request, ClaimsPrincipal user, IAuthService auth) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? user.FindFirst("sub")?.Value
                         ?? throw new UnauthorizedAccessException();
            var me = await auth.UpdateProfileAsync(userId, request);
            return Results.Ok(new { user = me });
        }).RequireAuthorization();

        return app;
    }
}
