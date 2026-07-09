using Tripkitty.Application.DTOs;
using Tripkitty.Application.Logic;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Services;

public interface IExpenseService
{
    Task<ExpenseDto> AddAsync(string tripId, string userId, AddExpenseRequest request);
    Task<ExpenseDto> UpdateAsync(string tripId, string userId, string expenseId, AddExpenseRequest request);
    Task RemoveAsync(string tripId, string userId, string expenseId);
    Task<SettlementsResponse> GetSettlementsAsync(string tripId, string userId);
}

public class ExpenseService(
    ITripRepository tripRepo,
    IPushNotificationService pushService,
    ITripNotifier notifier,
    IPaymentMethodRepository paymentMethodRepo) : IExpenseService
{
    public async Task<ExpenseDto> AddAsync(string tripId, string userId, AddExpenseRequest request)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        var shareEntries = ValidateAndBuildShare(trip, request);

        var expense = new Expense
        {
            Id = Guid.NewGuid().ToString("N"),
            TripId = tripId,
            Title = request.Title.Trim(),
            AmountMinor = (long)Math.Round(request.Amount * 100),
            Payer = request.Payer,
            Share = shareEntries,
            SplitType = request.SplitType,
            CreatedBy = userId
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

    public async Task<ExpenseDto> UpdateAsync(string tripId, string userId, string expenseId, AddExpenseRequest request)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        var expense = trip.Expenses.FirstOrDefault(e => e.Id == expenseId)
                      ?? throw new DomainException("NOT_FOUND", "Expense not found");

        var shareEntries = ValidateAndBuildShare(trip, request);

        expense.Title = request.Title.Trim();
        expense.AmountMinor = (long)Math.Round(request.Amount * 100);
        expense.Payer = request.Payer;
        expense.Share = shareEntries;
        expense.SplitType = request.SplitType;

        trip.Version++;
        await tripRepo.SaveChangesAsync();

        var dto = MapToDto(expense, request.Amount);
        await notifier.ExpenseUpdatedAsync(tripId, dto);
        return dto;
    }

    public async Task RemoveAsync(string tripId, string userId, string expenseId)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        var expense = trip.Expenses.FirstOrDefault(e => e.Id == expenseId)
                      ?? throw new DomainException("NOT_FOUND", "Expense not found");

        trip.Expenses.Remove(expense);
        trip.Version++;
        await tripRepo.SaveChangesAsync();

        _ = notifier.ExpenseRemovedAsync(tripId, expenseId);
    }

    public async Task<SettlementsResponse> GetSettlementsAsync(string tripId, string userId)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        var (balances, transactions) = SettlementsCalculator.Compute(trip.Expenses);

        // Реквизиты получателя: гость — его PaymentDetails; юзер — override в поездке,
        // иначе фолбэк на дефолтный способ оплаты из профиля.
        var receiverUserIds = transactions
            .Select(t => t.To)
            .Where(id => id.StartsWith("u_") &&
                         trip.Members.FirstOrDefault(m => m.UserId == id)?.PaymentDetails is null)
            .Distinct()
            .ToList();

        var fallbackMethods = await paymentMethodRepo.GetForUsersAsync(receiverUserIds);
        var defaultByUser = fallbackMethods
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.IsDefault).First());

        var settlements = transactions.Select(t =>
        {
            PaymentDetailsDto? toPayment = null;

            if (t.To.StartsWith("g_"))
            {
                var guest = trip.Guests.FirstOrDefault(g => g.Id == t.To);
                toPayment = PaymentDetailsDto.From(guest?.PaymentDetails);
            }
            else
            {
                var member = trip.Members.FirstOrDefault(m => m.UserId == t.To);
                if (member?.PaymentDetails is { } pd)
                    toPayment = PaymentDetailsDto.From(pd);
                else if (defaultByUser.TryGetValue(t.To, out var m))
                    toPayment = new PaymentDetailsDto(m.Phone, m.Banks, m.Label);
            }

            return new SettlementDto(t.From, t.To, t.Amount, toPayment);
        }).ToList();

        return new SettlementsResponse(balances, settlements);
    }

    private static List<ShareEntry> ValidateAndBuildShare(Trip trip, AddExpenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new DomainException("VALIDATION_ERROR", "Title is required", "title");

        if (request.Amount <= 0)
            throw new DomainException("VALIDATION_ERROR", "Amount must be positive", "amount");

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
            expense.CreatedBy
        );

    private static HashSet<string> GetAllParticipantIds(Trip trip)
    {
        var ids = new HashSet<string>();
        foreach (var m in trip.Members) ids.Add(m.UserId);
        foreach (var g in trip.Guests) ids.Add(g.Id);
        return ids;
    }
}
