using System.Security.Claims;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin");

        group.MapGet("/stats", async (ClaimsPrincipal user, IAdminStatsService stats, IConfiguration config) =>
        {
            var email = user.FindFirst(ClaimTypes.Email)?.Value
                        ?? user.FindFirst("email")?.Value
                        ?? throw new UnauthorizedAccessException();

            var admins = config.GetSection("Admin:Emails").Get<string[]>() ?? [];
            if (!admins.Contains(email, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("FORBIDDEN", "Доступ только для администратора");

            var result = await stats.GetStatsAsync();
            return Results.Ok(new { stats = result });
        }).RequireAuthorization();

        return app;
    }
}
