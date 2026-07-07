using Tripkitty.Application.DTOs;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Logic;

// Строит валидированные реквизиты из запроса. Единая точка валидации телефона и банков —
// используется для гостя, реквизитов участника в поездке и способов оплаты профиля.
public static class PaymentDetailsFactory
{
    public static PaymentDetails? FromRequest(PaymentDetailsRequest? req)
    {
        if (req is null)
            return null;

        return new PaymentDetails
        {
            Phone = PhoneNormalizer.Normalize(req.Phone),
            Banks = ValidateBanks(req.Banks),
            Label = string.IsNullOrWhiteSpace(req.Label) ? null : req.Label.Trim()
        };
    }

    public static List<string> ValidateBanks(List<string>? banks, string field = "banks")
    {
        if (banks is null || banks.Count == 0)
            throw new DomainException("VALIDATION_ERROR", "Укажите хотя бы один банк", field);

        var normalized = banks.Distinct().ToList();
        var unknown = normalized.FirstOrDefault(b => !BankCatalog.IsValid(b));
        if (unknown is not null)
            throw new DomainException("INVALID_BANK", $"Неизвестный банк: {unknown}", field);

        return normalized;
    }
}
