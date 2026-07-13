using Tripkitty.Application.DTOs;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Services;

public interface IExpenseService
{
    Task<ExpenseDto> AddAsync(string tripId, string userId, AddExpenseRequest request);
    Task<(ExpenseDto Expense, bool TripHasPaidTransfers)> UpdateAsync(string tripId, string userId, string expenseId, AddExpenseRequest request);
    Task RemoveAsync(string tripId, string userId, string expenseId);
}

public class ExpenseService(
    ITripRepository tripRepo,
    IPushNotificationService pushService,
    ITripNotifier notifier) : IExpenseService
{
    public async Task<ExpenseDto> AddAsync(string tripId, string userId, AddExpenseRequest request)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        EnsureActive(trip);

        var shareEntries = ValidateAndBuildShare(trip, request);
        var sponsors = BuildSponsors(trip, request.Sponsors, existing: null);

        var expense = new Expense
        {
            Id = Guid.NewGuid().ToString("N"),
            TripId = tripId,
            Title = request.Title.Trim(),
            AmountMinor = (long)Math.Round(request.Amount * 100),
            Payer = request.Payer,
            Share = shareEntries,
            SplitType = request.SplitType,
            Sponsors = sponsors,
            CreatedBy = userId,
            GrossAmountMinor = request.GrossAmount.HasValue ? (long)Math.Round(request.GrossAmount.Value * 100) : null,
            DiscountPercent = request.DiscountPercent,
            DiscountAmountMinor = request.DiscountAmount.HasValue ? (long)Math.Round(request.DiscountAmount.Value * 100) : null
        };

        trip.Expenses.Add(expense);
        trip.Version++;
        await tripRepo.SaveChangesAsync();

        var otherMemberIds = trip.Members
            .Select(m => m.UserId)
            .Where(id => id != userId)
            .ToList();

        if (otherMemberIds.Count > 0)
            await pushService.NotifyManyAsync(otherMemberIds, "Новый расход",
                $"{trip.Name}: {expense.Title} — {request.Amount:F2}");

        var dto = MapToDto(expense, request.Amount);
        await notifier.ExpenseAddedAsync(tripId, dto);
        return dto;
    }

    public async Task<(ExpenseDto Expense, bool TripHasPaidTransfers)> UpdateAsync(string tripId, string userId, string expenseId, AddExpenseRequest request)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        EnsureActive(trip);

        var expense = trip.Expenses.FirstOrDefault(e => e.Id == expenseId)
                      ?? throw new DomainException("NOT_FOUND", "Expense not found");

        if (expense.IsTransfer)
            throw new DomainException("TRANSFER_READONLY", "Перевод нельзя редактировать или удалять");

        var shareEntries = ValidateAndBuildShare(trip, request);

        expense.Title = request.Title.Trim();
        expense.AmountMinor = (long)Math.Round(request.Amount * 100);
        expense.Payer = request.Payer;
        expense.Share = shareEntries;
        expense.SplitType = request.SplitType;
        expense.Sponsors = BuildSponsors(trip, request.Sponsors, expense.Sponsors);
        expense.GrossAmountMinor = request.GrossAmount.HasValue ? (long)Math.Round(request.GrossAmount.Value * 100) : null;
        expense.DiscountPercent = request.DiscountPercent;
        expense.DiscountAmountMinor = request.DiscountAmount.HasValue ? (long)Math.Round(request.DiscountAmount.Value * 100) : null;

        trip.Version++;
        await tripRepo.SaveChangesAsync();

        var dto = MapToDto(expense, request.Amount);
        await notifier.ExpenseUpdatedAsync(tripId, dto);

        var tripHasPaidTransfers = trip.Expenses.Any(e => e.IsTransfer);
        return (dto, tripHasPaidTransfers);
    }

    public async Task RemoveAsync(string tripId, string userId, string expenseId)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        EnsureActive(trip);

        var expense = trip.Expenses.FirstOrDefault(e => e.Id == expenseId)
                      ?? throw new DomainException("NOT_FOUND", "Expense not found");

        if (expense.IsTransfer)
            throw new DomainException("TRANSFER_READONLY", "Перевод нельзя редактировать или удалять");

        trip.Expenses.Remove(expense);
        trip.Version++;
        await tripRepo.SaveChangesAsync();

        _ = notifier.ExpenseRemovedAsync(tripId, expenseId);
    }

    private static void EnsureActive(Trip trip)
    {
        if (trip.Status != TripStatus.Active)
            throw new DomainException("TRIP_SETTLING",
                "Подсчёт завершён — изменения заблокированы. Переоткройте подсчёт, чтобы вносить правки");
    }

    private static List<ShareEntry> ValidateAndBuildShare(Trip trip, AddExpenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new DomainException("VALIDATION_ERROR", "Title is required", "title");

        if (request.Amount <= 0)
            throw new DomainException("VALIDATION_ERROR", "Amount must be positive", "amount");

        ValidateDiscount(request);

        if (string.IsNullOrWhiteSpace(request.Payer))
            throw new DomainException("VALIDATION_ERROR", "Payer is required", "payer");

        if (request.Share is null || request.Share.Count == 0)
            throw new DomainException("VALIDATION_ERROR", "Share must have at least one participant", "share");

        ValidateSplitType(request);

        var allParticipantIds = GetAllParticipantIds(trip);

        if (!allParticipantIds.Contains(request.Payer))
            throw new DomainException("INVALID_PAYER", "Payer is not a participant in this trip", "payer");

        var invalidShare = request.Share.Where(s => !allParticipantIds.Contains(s.ParticipantId)).ToList();
        if (invalidShare.Count > 0)
            throw new DomainException("INVALID_SHARE", "Some share participants are not in this trip", "share");

        return request.Share
            .GroupBy(s => s.ParticipantId)
            .Select(g => g.First())
            .Select(s => new ShareEntry
            {
                ParticipantId = s.ParticipantId,
                Weight = s.Weight,
                AmountMinor = s.Amount.HasValue ? (long)Math.Round(s.Amount.Value * 100) : null
            })
            .ToList();
    }

    private static void ValidateDiscount(AddExpenseRequest request)
    {
        if (request.DiscountPercent.HasValue && request.DiscountAmount.HasValue)
            throw new DomainException("VALIDATION_ERROR",
                "Specify either discount percent or discount amount, not both", "discount");

        if (!request.DiscountPercent.HasValue && !request.DiscountAmount.HasValue)
            return;

        if (!request.GrossAmount.HasValue || request.GrossAmount.Value <= 0)
            throw new DomainException("VALIDATION_ERROR",
                "Gross amount is required when a discount is set", "grossAmount");

        if (request.DiscountPercent is < 0 or > 100)
            throw new DomainException("VALIDATION_ERROR",
                "Discount percent must be between 0 and 100", "discountPercent");

        if (request.DiscountAmount is < 0)
            throw new DomainException("VALIDATION_ERROR",
                "Discount amount must not be negative", "discountAmount");

        var expectedNet = request.DiscountPercent.HasValue
            ? request.GrossAmount.Value * (1 - request.DiscountPercent.Value / 100m)
            : request.GrossAmount.Value - request.DiscountAmount!.Value;

        if (Math.Abs(expectedNet - request.Amount) > 0.01m)
            throw new DomainException("VALIDATION_ERROR",
                $"Amount after discount ({expectedNet:F2}) must equal total amount ({request.Amount:F2})", "amount");
    }

    private static void ValidateSplitType(AddExpenseRequest request)
    {
        switch (request.SplitType)
        {
            case SplitType.ByShares:
                if (request.Share.Any(s => !s.Weight.HasValue || s.Weight.Value <= 0))
                    throw new DomainException("VALIDATION_ERROR",
                        "All share entries must have a positive weight for ByShares split", "share");
                break;

            case SplitType.ByAmounts:
                if (request.Share.Any(s => !s.Amount.HasValue || s.Amount.Value <= 0))
                    throw new DomainException("VALIDATION_ERROR",
                        "All share entries must have a positive amount for ByAmounts split", "share");

                var shareSum = request.Share.Sum(s => s.Amount!.Value);
                if (Math.Abs(shareSum - request.Amount) > 0.01m)
                    throw new DomainException("VALIDATION_ERROR",
                        $"Sum of share amounts ({shareSum:F2}) must equal total amount ({request.Amount:F2})", "share");
                break;
        }
    }

    // Общий бюджет расхода: null от клиента = снапшот живого спонсорства (при создании)
    // либо оставить как было (при правке). Явная карта — только живые пары спонсорства
    // плюс уже записанные на этом расходе: нельзя «повесить» расход на того, кто
    // плательщиком быть не соглашался.
    private static Dictionary<string, string> BuildSponsors(
        Trip trip, Dictionary<string, string>? requested, Dictionary<string, string>? existing)
    {
        var live = LiveSponsorMap(trip);

        if (requested is null)
            return existing ?? live;

        foreach (var (dependent, sponsor) in requested)
        {
            var allowed = (live.TryGetValue(dependent, out var liveSponsor) && liveSponsor == sponsor)
                          || (existing is not null && existing.TryGetValue(dependent, out var oldSponsor) && oldSponsor == sponsor);
            if (!allowed)
                throw new DomainException("INVALID_SPONSORS",
                    "Такой пары общего бюджета нет ни в поездке, ни в этом расходе", "sponsors");
        }

        return new Dictionary<string, string>(requested);
    }

    private static Dictionary<string, string> LiveSponsorMap(Trip trip)
    {
        var map = new Dictionary<string, string>();
        foreach (var m in trip.Members.Where(m => m.SponsorId is not null))
            map[m.UserId] = m.SponsorId!;
        foreach (var g in trip.Guests.Where(g => g.SponsorId is not null))
            map[g.Id] = g.SponsorId!;
        return map;
    }

    private static ExpenseDto MapToDto(Expense expense, decimal amount) =>
        new(
            expense.Id,
            expense.Title,
            amount,
            expense.Payer,
            expense.Share.Select(s => new ShareEntryDto(
                s.ParticipantId,
                s.Weight,
                s.AmountMinor.HasValue ? s.AmountMinor.Value / 100m : null
            )).ToList(),
            expense.SplitType,
            expense.CreatedBy,
            expense.IsTransfer,
            expense.GrossAmountMinor.HasValue ? expense.GrossAmountMinor.Value / 100m : null,
            expense.DiscountPercent,
            expense.DiscountAmountMinor.HasValue ? expense.DiscountAmountMinor.Value / 100m : null,
            expense.Sponsors
        );

    private static HashSet<string> GetAllParticipantIds(Trip trip)
    {
        var ids = new HashSet<string>();
        foreach (var m in trip.Members) ids.Add(m.UserId);
        foreach (var g in trip.Guests) ids.Add(g.Id);
        return ids;
    }
}
