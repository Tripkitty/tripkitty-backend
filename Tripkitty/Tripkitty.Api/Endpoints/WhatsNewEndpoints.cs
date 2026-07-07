using Tripkitty.Application.DTOs;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Api.Endpoints;

public static class WhatsNewEndpoints
{
    public static IEndpointRouteBuilder MapWhatsNewEndpoints(this IEndpointRouteBuilder app)
    {
        // Публичный: плашку «Что нового» должны видеть и незалогиненные.
        // since — последняя версия, которую видел клиент; отдаём только релизы новее неё.
        // Без since отдаём все релизы (полная история изменений).
        app.MapGet("/whats-new", (int? since) =>
        {
            var releases = WhatsNewCatalog.Releases
                .Where(r => since is null || r.Version > since)
                .OrderByDescending(r => r.Version)
                .Select(WhatsNewReleaseDto.From)
                .ToList();

            var whatsNew = new WhatsNewDto(WhatsNewCatalog.LatestVersion, releases);
            return Results.Ok(new { whatsNew });
        }).AllowAnonymous();

        return app;
    }
}
