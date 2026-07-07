using Tripkitty.Domain.Entities;

namespace Tripkitty.Application.DTOs;

public record WhatsNewReleaseDto(int Version, string Title, DateOnly Date, IReadOnlyList<string> Items)
{
    public static WhatsNewReleaseDto From(WhatsNewRelease r) => new(r.Version, r.Title, r.Date, r.Items);
}

// LatestVersion — старшая доступная версия (для сравнения с сохранённой на клиенте),
// Releases — релизы новее since (или все), отсортированные от новых к старым.
public record WhatsNewDto(int LatestVersion, IReadOnlyList<WhatsNewReleaseDto> Releases);
