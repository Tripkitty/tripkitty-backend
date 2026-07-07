using Tripkitty.Domain.Entities;

namespace Tripkitty.Application.DTOs;

// Реквизиты получателя (номер + список банков) — для гостя, участника поездки и settlements.
public record PaymentDetailsDto(string Phone, List<string> Banks, string? Label)
{
    public static PaymentDetailsDto? From(PaymentDetails? p) =>
        p is null ? null : new PaymentDetailsDto(p.Phone, p.Banks, p.Label);
}

public record PaymentDetailsRequest(string Phone, List<string> Banks, string? Label = null);

// Эффективные реквизиты текущего юзера в поездке: источник — override поездки или дефолт профиля.
public record TripPaymentDto(PaymentDetailsDto? Payment, string Source); // Source: "trip" | "profile" | "none"

// Задать/сбросить свои реквизиты в поездке. Payment = null → сброс на дефолт профиля.
public record SetTripPaymentRequest(PaymentDetailsRequest? Payment);

// Способ оплаты в профиле пользователя.
public record PaymentMethodDto(string Id, string Phone, List<string> Banks, string? Label, bool IsDefault)
{
    public static PaymentMethodDto From(PaymentMethod p) =>
        new(p.Id, p.Phone, p.Banks, p.Label, p.IsDefault);
}

public record CreatePaymentMethodRequest(string Phone, List<string> Banks, string? Label = null, bool IsDefault = false);

public record UpdatePaymentMethodRequest(string? Phone, List<string>? Banks, string? Label, bool? IsDefault);

public record BankDto(string Code, string Name);
