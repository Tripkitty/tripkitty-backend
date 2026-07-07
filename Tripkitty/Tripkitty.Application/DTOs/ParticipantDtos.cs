namespace Tripkitty.Application.DTOs;

public record AddMemberRequest(string UserId);

public record AddGuestRequest(string LastName, string FirstName, string? MiddleName, PaymentDetailsRequest? PaymentDetails = null);

// Частичное обновление гостя. ФИО: null = не менять (MiddleName пустая строка = сбросить).
// Реквизиты: PaymentDetails задаёт/меняет их; ClearPayment = true сбрасывает; иначе не трогаем.
public record UpdateGuestRequest(
    string? LastName,
    string? FirstName,
    string? MiddleName,
    PaymentDetailsRequest? PaymentDetails = null,
    bool ClearPayment = false);
