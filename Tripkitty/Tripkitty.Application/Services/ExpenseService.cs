using Tripkitty.Application.DTOs;
using Tripkitty.Application.Logic;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Services;

public interface IExpenseService
{
    Task<ExpenseDto> AddAsync(string tripId, string userId, AddExpenseRequest request);
    Task RemoveAsync(string tripId, string userId, string expenseId);
    Task<SettlementsResponse> GetSettlementsAsync(string tripId, string userId);
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

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new DomainException("VALIDATION_ERROR", "Title is required", "title");

        if (request.Amount <= 0)
            throw new DomainException("VALIDATION_ERROR", "Amount must be positive", "amount");

        if (string.IsNullOrWhiteSpace(request.Payer))
            throw new DomainException("VALIDATION_ERROR", "Payer is required", "payer");

        if (request.Share is null || request.Share.Count == 0)
            throw new DomainException("VALIDATION_ERROR", "Share must have at least one participant", "share");

        // Validate payer and share are trip participants
        var allParticipantIds = GetAllParticipantIds(trip);

        if (!allParticipantIds.Contains(request.Payer))
            throw new DomainException("INVALID_PAYER", "Payer is not a participant in this trip", "payer");

        var invalidShare = request.Share.Where(s => !allParticipantIds.Contains(s)).ToList();
        if (invalidShare.Count > 0)
            throw new DomainException("INVALID_SHARE", "Some share participants are not in this trip", "share");

        var expense = new Expense
        {
            Id = Guid.NewGuid().ToString("N"),
            TripId = tripId,
            Title = request.Title.Trim(),
            AmountMinor = (long)Math.Round(request.Amount * 100),
            Payer = request.Payer,
            Share = request.Share.Distinct().ToList(),
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

        var dto = new ExpenseDto(expense.Id, expense.Title, request.Amount, expense.Payer, expense.Share, expense.CreatedBy);
        _ = notifier.ExpenseAddedAsync(tripId, dto);
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

        return new SettlementsResponse(
            balances,
            transactions.Select(t => new SettlementDto(t.From, t.To, t.Amount)).ToList()
        );
    }

    private static HashSet<string> GetAllParticipantIds(Trip trip)
    {
        var ids = new HashSet<string>();
        foreach (var m in trip.Members) ids.Add(m.UserId);
        foreach (var g in trip.Guests) ids.Add(g.Id);
        return ids;
    }
}
