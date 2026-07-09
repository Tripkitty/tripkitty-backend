using System.Net;
using System.Text.Json;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    private static readonly HashSet<string> ConflictCodes = new()
    {
        "HANDLE_TAKEN", "EMAIL_TAKEN", "ALREADY_FRIENDS", "REQUEST_EXISTS", "ALREADY_MEMBER", "PARTICIPANT_HAS_EXPENSES",
        "TRIP_SETTLING", "ALREADY_FINALIZED", "NOT_FINALIZED", "TRANSFER_READONLY"
    };

    private static readonly HashSet<string> NotFoundCodes = new()
    {
        "NOT_FOUND", "PAYMENT_METHOD_NOT_FOUND", "GUEST_NOT_FOUND", "TRANSACTION_NOT_FOUND"
    };

    private static readonly HashSet<string> ForbiddenCodes = new()
    {
        "FORBIDDEN"
    };

    private static readonly HashSet<string> UnprocessableCodes = new()
    {
        "SELF_REQUEST", "INVALID_PAYER", "INVALID_SHARE", "USER_NOT_FOUND",
        "WRONG_PASSWORD", "INVALID_TOKEN", "VERSION_CONFLICT",
        "INVALID_PHONE", "INVALID_BANK"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            var statusCode = GetStatusCode(ex.Code);
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = ex.Code,
                    message = ex.Message,
                    field = ex.Field,
                    details = ex.Details
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = "An unexpected error occurred",
                    field = (string?)null
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
        }
    }

    private static HttpStatusCode GetStatusCode(string code)
    {
        if (ConflictCodes.Contains(code)) return HttpStatusCode.Conflict;
        if (NotFoundCodes.Contains(code)) return HttpStatusCode.NotFound;
        if (ForbiddenCodes.Contains(code)) return HttpStatusCode.Forbidden;
        if (UnprocessableCodes.Contains(code)) return HttpStatusCode.UnprocessableEntity;
        if (code == "VALIDATION_ERROR") return HttpStatusCode.BadRequest;
        return HttpStatusCode.InternalServerError;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
