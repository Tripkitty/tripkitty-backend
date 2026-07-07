using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        // Справочник банков — публичный, чтобы фронт мог рисовать список без релиза.
        app.MapGet("/banks", () =>
        {
            var banks = BankCatalog.All.Select(kv => new BankDto(kv.Key, kv.Value)).ToList();
            return Results.Ok(new { banks });
        }).AllowAnonymous();

        var group = app.MapGroup("/me/payment-methods").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IPaymentMethodService service) =>
        {
            var userId = TripEndpoints.GetUserId(user);
            var paymentMethods = await service.GetMineAsync(userId);
            return Results.Ok(new { paymentMethods });
        });

        group.MapPost("/", async (CreatePaymentMethodRequest request, ClaimsPrincipal user,
            IPaymentMethodService service) =>
        {
            var userId = TripEndpoints.GetUserId(user);
            var paymentMethod = await service.CreateAsync(userId, request);
            return Results.Created($"/me/payment-methods/{paymentMethod.Id}", new { paymentMethod });
        });

        group.MapPatch("/{id}", async (string id, UpdatePaymentMethodRequest request, ClaimsPrincipal user,
            IPaymentMethodService service) =>
        {
            var userId = TripEndpoints.GetUserId(user);
            var paymentMethod = await service.UpdateAsync(userId, id, request);
            return Results.Ok(new { paymentMethod });
        });

        group.MapDelete("/{id}", async (string id, ClaimsPrincipal user, IPaymentMethodService service) =>
        {
            var userId = TripEndpoints.GetUserId(user);
            await service.DeleteAsync(userId, id);
            return Results.Ok(new { message = "Payment method deleted" });
        });

        return app;
    }
}
