namespace Tripkitty.Domain.Entities;

// Статический каталог релизов «Что нового». Отдаётся фронту через GET /whats-new.
// Как добавить релиз: допиши новый элемент В НАЧАЛО массива с Version = предыдущий + 1,
// закоммить и передеплой бэка (docker compose up -d --build).
// Version инкрементится ВРУЧНУЮ и только для значимых юзер-facing изменений — не на каждый деплой.
public static class WhatsNewCatalog
{
    public static readonly IReadOnlyList<WhatsNewRelease> Releases = new[]
    {
        new WhatsNewRelease(3, "Оплата по СБП", new DateOnly(2026, 7, 7), new[]
        {
            "Реквизиты для перевода теперь можно указать в профиле",
            "В расчётах виден банк и телефон получателя",
        }),
        new WhatsNewRelease(2, "Редактирование профиля", new DateOnly(2026, 6, 20), new[]
        {
            "Можно менять имя, фамилию и отчество",
            "Гостям поездки можно задавать реквизиты для перевода",
        }),
        new WhatsNewRelease(1, "Добро пожаловать в Tripkitty 🐱", new DateOnly(2026, 6, 1), new[]
        {
            "Создавайте поездки и делите расходы с друзьями",
        }),
    };

    // Наибольшая версия — клиент сравнивает со своей сохранённой (localStorage).
    public static int LatestVersion => Releases.Count == 0 ? 0 : Releases.Max(r => r.Version);
}

public record WhatsNewRelease(int Version, string Title, DateOnly Date, IReadOnlyList<string> Items);
