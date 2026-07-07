using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Logic;

// Нормализация телефона к каноническому RU-формату +7XXXXXXXXXX.
// Пока поддерживаются только российские номера.
public static class PhoneNormalizer
{
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new DomainException("INVALID_PHONE", "Укажите номер телефона", "phone");

        var digits = new string(raw.Where(char.IsDigit).ToArray());

        // 8XXXXXXXXXX / 7XXXXXXXXXX → отбрасываем код страны
        if (digits.Length == 11 && (digits[0] == '7' || digits[0] == '8'))
            digits = digits[1..];

        if (digits.Length != 10)
            throw new DomainException("INVALID_PHONE", "Укажите корректный российский номер (+7)", "phone");

        return "+7" + digits;
    }
}
