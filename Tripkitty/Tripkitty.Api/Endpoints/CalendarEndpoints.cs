using Tripkitty.Application.Services;
using Tripkitty.Domain.Exceptions;
using Tripkitty.Infrastructure.Services;

namespace Tripkitty.Api.Endpoints;

public static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        // Public subscription feed — authenticated via token embedded in URL
        app.MapGet("/calendar/{token}.ics", async (string token, ITripRepository tripRepo, IIcsService icsService) =>
        {
            var trip = await tripRepo.GetByCalendarTokenAsync(token)
                       ?? throw new DomainException("NOT_FOUND", "Calendar not found");

            var ics = icsService.GenerateIcs(trip);

            return Results.Content(ics, "text/calendar; charset=utf-8");
        }).AllowAnonymous();

        return app;
    }
}
