namespace Tripkitty.Domain.Entities;

// Справочник поддерживаемых банков СБП: код -> отображаемое имя.
// Отдаётся фронту через GET /banks, коды хранятся в PaymentMethod.Banks и PaymentDetails.Banks.
public static class BankCatalog
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        ["SBERBANK"] = "Сбербанк",
        ["TBANK"] = "Т-Банк",
        ["ALFABANK"] = "Альфа-Банк",
        ["VTB"] = "ВТБ",
    };

    public static bool IsValid(string code) => All.ContainsKey(code);
}
