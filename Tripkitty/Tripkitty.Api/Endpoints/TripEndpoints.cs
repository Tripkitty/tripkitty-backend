using System.Security.Claims;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Exceptions;
using Tripkitty.Infrastructure.Services;

namespace Tripkitty.Api.Endpoints;

public static class TripEndpoints
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/trips").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, ITripService tripService) =>
        {
            var userId = GetUserId(user);
            var trips = await tripService.GetAllAsync(userId);
            return Results.Ok(new { trips });
        });

        group.MapPost("/", async (CreateTripRequest request, ClaimsPrincipal user, ITripService tripService) =>
        {
            var userId = GetUserId(user);
            var trip = await tripService.CreateAsync(request, userId);
            return Results.Created($"/trips/{trip.Id}", new { trip });
        });

        group.MapGet("/{id}", async (string id, ClaimsPrincipal user, ITripService tripService) =>
        {
            var userId = GetUserId(user);
            var trip = await tripService.GetByIdAsync(id, userId);
            return Results.Ok(new { trip });
        });

        group.MapPatch("/{id}", async (string id, PatchTripRequest request, ClaimsPrincipal user,
            HttpRequest httpRequest, ITripService tripService) =>
        {
            var userId = GetUserId(user);

            if (!httpRequest.Headers.TryGetValue("If-Match", out var ifMatchHeader)
                || !long.TryParse(ifMatchHeader.ToString().Trim('"'), out var version))
            {
                throw new DomainException("VERSION_CONFLICT", "If-Match header with trip version is required");
            }

            var trip = await tripService.PatchAsync(id, userId, request, version);
            return Results.Ok(new { trip });
        });

        group.MapPost("/{id}/clear", async (string id, ClaimsPrincipal user, ITripService tripService) =>
        {
            var userId = GetUserId(user);
            await tripService.ClearAsync(id, userId);
            return Results.Ok(new { message = "Trip cleared" });
        });

        group.MapDelete("/{id}", async (string id, ClaimsPrincipal user, ITripService tripService) =>
        {
            var userId = GetUserId(user);
            await tripService.DeleteAsync(id, userId);
            return Results.Ok(new { message = "Trip deleted" });
        });

        // Participant endpoints
        group.MapPost("/{id}/members", async (string id, AddMemberRequest request, ClaimsPrincipal user,
            IParticipantService participantService) =>
        {
            var userId = GetUserId(user);
            var member = await participantService.AddMemberAsync(id, userId, request.UserId);
            return Results.Ok(new { member });
        });

        group.MapPost("/{id}/guests", async (string id, AddGuestRequest request, ClaimsPrincipal user,
            IParticipantService participantService) =>
        {
            var userId = GetUserId(user);
            var guest = await participantService.AddGuestAsync(id, userId, request);
            return Results.Ok(new { guest });
        });

        group.MapPatch("/{id}/guests/{guestId}", async (string id, string guestId, UpdateGuestRequest request,
            ClaimsPrincipal user, IParticipantService participantService) =>
        {
            var userId = GetUserId(user);
            var guest = await participantService.UpdateGuestAsync(id, userId, guestId, request);
            return Results.Ok(new { guest });
        });

        group.MapDelete("/{id}/participants/{participantId}", async (string id, string participantId,
            ClaimsPrincipal user, IParticipantService participantService) =>
        {
            var userId = GetUserId(user);
            await participantService.RemoveParticipantAsync(id, userId, participantId);
            return Results.Ok(new { message = "Participant removed" });
        });

        // Expense endpoints
        group.MapPost("/{id}/expenses", async (string id, AddExpenseRequest request, ClaimsPrincipal user,
            IExpenseService expenseService) =>
        {
            var userId = GetUserId(user);
            var expense = await expenseService.AddAsync(id, userId, request);
            return Results.Ok(new { expense });
        });

        group.MapPatch("/{id}/expenses/{expenseId}", async (string id, string expenseId, AddExpenseRequest request,
            ClaimsPrincipal user, IExpenseService expenseService) =>
        {
            var userId = GetUserId(user);
            var (expense, tripHasPaidTransfers) = await expenseService.UpdateAsync(id, userId, expenseId, request);
            return Results.Ok(new
            {
                expense,
                warning = tripHasPaidTransfers ? "TRIP_HAS_PAID_TRANSFERS" : null
            });
        });

        group.MapDelete("/{id}/expenses/{expenseId}", async (string id, string expenseId,
            ClaimsPrincipal user, IExpenseService expenseService) =>
        {
            var userId = GetUserId(user);
            await expenseService.RemoveAsync(id, userId, expenseId);
            return Results.Ok(new { message = "Expense removed" });
        });

        group.MapGet("/{id}/settlements", async (string id, ClaimsPrincipal user, ISettlementService settlementService) =>
        {
            var userId = GetUserId(user);
            var settlements = await settlementService.GetAsync(id, userId);
            return Results.Ok(settlements);
        });

        // Финализация подсчёта: фиксирует транзакции, блокирует расходы и состав участников
        group.MapPost("/{id}/settlement", async (string id, ClaimsPrincipal user,
            ISettlementService settlementService) =>
        {
            var userId = GetUserId(user);
            var settlements = await settlementService.FinalizeAsync(id, userId);
            return Results.Ok(new { settlements });
        });

        group.MapPost("/{id}/settlement/reopen", async (string id, ClaimsPrincipal user,
            ISettlementService settlementService) =>
        {
            var userId = GetUserId(user);
            var settlements = await settlementService.ReopenAsync(id, userId);
            return Results.Ok(new { settlements });
        });

        group.MapPatch("/{id}/settlement/transactions/{txId}", async (string id, string txId,
            SetTransactionPaidRequest request, ClaimsPrincipal user, ISettlementService settlementService) =>
        {
            var userId = GetUserId(user);
            var settlements = await settlementService.SetPaidAsync(id, userId, txId, request.Paid);
            return Results.Ok(new { settlements });
        });

        // Мои реквизиты для перевода в этой поездке (override поездки ?? дефолт профиля)
        group.MapGet("/{id}/me/payment", async (string id, ClaimsPrincipal user,
            IParticipantService participantService) =>
        {
            var userId = GetUserId(user);
            var payment = await participantService.GetMyPaymentAsync(id, userId);
            return Results.Ok(payment);
        });

        group.MapPatch("/{id}/me/payment", async (string id, SetTripPaymentRequest request, ClaimsPrincipal user,
            IParticipantService participantService) =>
        {
            var userId = GetUserId(user);
            var payment = await participantService.SetMyPaymentAsync(id, userId, request.Payment);
            return Results.Ok(payment);
        });

        // Event endpoints
        group.MapPost("/{id}/events", async (string id, AddEventRequest request, ClaimsPrincipal user,
            IEventService eventService) =>
        {
            var userId = GetUserId(user);
            var ev = await eventService.AddAsync(id, userId, request);
            return Results.Ok(new { @event = ev });
        });

        group.MapPatch("/{id}/events/{eventId}", async (string id, string eventId, AddEventRequest request,
            ClaimsPrincipal user, IEventService eventService) =>
        {
            var userId = GetUserId(user);
            var ev = await eventService.UpdateAsync(id, userId, eventId, request);
            return Results.Ok(new { @event = ev });
        });

        group.MapDelete("/{id}/events/{eventId}", async (string id, string eventId,
            ClaimsPrincipal user, IEventService eventService) =>
        {
            var userId = GetUserId(user);
            await eventService.RemoveAsync(id, userId, eventId);
            return Results.Ok(new { message = "Event removed" });
        });

        // ICS calendar download (requires auth)
        group.MapGet("/{id}/calendar.ics", async (string id, ClaimsPrincipal user,
            HttpContext context, ITripRepository tripRepo, IIcsService icsService) =>
        {
            var userId = GetUserId(user);
            var trip = await tripRepo.GetByIdWithDetailsAsync(id)
                       ?? throw new DomainException("NOT_FOUND", "Trip not found");

            var isMember = trip.Members.Any(m => m.UserId == userId);
            if (!isMember)
                throw new DomainException("FORBIDDEN", "You are not a member of this trip");

            var ics = icsService.GenerateIcs(trip);
            var safeName = string.Concat(trip.Name.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_'));
            context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{safeName}.ics\"";

            return Results.Content(ics, "text/calendar; charset=utf-8");
        });

        // Calendar subscription URL for this user (webcal:// link for Apple Calendar)
        group.MapGet("/{id}/calendar-url", async (string id, ClaimsPrincipal user,
            HttpRequest httpRequest, ITripRepository tripRepo) =>
        {
            var userId = GetUserId(user);
            var trip = await tripRepo.GetByIdWithDetailsAsync(id)
                       ?? throw new DomainException("NOT_FOUND", "Trip not found");

            var member = trip.Members.FirstOrDefault(m => m.UserId == userId)
                         ?? throw new DomainException("FORBIDDEN", "You are not a member of this trip");

            var baseUrl = $"{httpRequest.Scheme}://{httpRequest.Host}";
            var httpsUrl = $"{baseUrl}/calendar/{member.CalendarToken}.ics";
            var webcalUrl = httpsUrl.Replace("https://", "webcal://").Replace("http://", "webcal://");

            return Results.Ok(new { url = webcalUrl, httpsUrl });
        });

        return app;
    }

    internal static string GetUserId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("User ID not found in token");
}
