using Tripkitty.Application.DTOs;
using Tripkitty.Application.Logic;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Services;

public interface ISettlementService
{
    Task<SettlementsResponse> GetAsync(string tripId, string userId);
    Task<SettlementsResponse> FinalizeAsync(string tripId, string userId);
    Task<SettlementsResponse> ReopenAsync(string tripId, string userId);
    Task<SettlementsResponse> SetPaidAsync(string tripId, string userId, string transactionId, bool paid);
}

public class SettlementService(
    ITripRepository tripRepo,
    IPushNotificationService pushService,
    ITripNotifier notifier,
    IPaymentMethodRepository paymentMethodRepo) : ISettlementService
{
    public async Task<SettlementsResponse> GetAsync(string tripId, string userId)
    {
        var trip = await GetTripForMemberAsync(tripId, userId);
        return await BuildResponseAsync(trip);
    }

    public async Task<SettlementsResponse> FinalizeAsync(string tripId, string userId)
    {
        var trip = await GetTripForMemberAsync(tripId, userId);

        if (trip.OwnerId != userId)
            throw new DomainException("FORBIDDEN", "Только владелец поездки может завершить подсчёт");

        if (trip.Status != TripStatus.Active)
            throw new DomainException("ALREADY_FINALIZED", "Подсчёт уже завершён");

        var (_, transactions) = SettlementsCalculator.Compute(trip.Expenses);

        foreach (var t in transactions)
        {
            trip.Settlements.Add(new SettlementTransaction
            {
                TripId = trip.Id,
                FromId = t.From,
                ToId = t.To,
                AmountMinor = (long)Math.Round(t.Amount * 100)
            });
        }

        // Все и так в расчёте — переводов нет, поездка сразу закрыта
        trip.Status = transactions.Count == 0 ? TripStatus.Settled : TripStatus.Settling;
        trip.Version++;
        await tripRepo.SaveChangesAsync();

        var otherMemberIds = trip.Members
            .Select(m => m.UserId)
            .Where(id => id != userId)
            .ToList();

        if (otherMemberIds.Count > 0)
            await pushService.NotifyManyAsync(otherMemberIds, "Подсчёт завершён",
                $"{trip.Name}: итоговый расчёт готов");

        return await NotifyAndBuildAsync(trip);
    }

    public async Task<SettlementsResponse> ReopenAsync(string tripId, string userId)
    {
        var trip = await GetTripForMemberAsync(tripId, userId);

        if (trip.OwnerId != userId)
            throw new DomainException("FORBIDDEN", "Только владелец поездки может переоткрыть подсчёт");

        if (trip.Status == TripStatus.Active)
            throw new DomainException("NOT_FINALIZED", "Подсчёт ещё не завершён");

        // Оплаченные транзакции превращаются в расходы-переводы, чтобы уже
        // переведённые деньги остались учтёнными в новом пересчёте.
        foreach (var tx in trip.Settlements.Where(s => s.IsPaid))
        {
            trip.Expenses.Add(new Expense
            {
                Id = Guid.NewGuid().ToString("N"),
                TripId = trip.Id,
                Title = "Перевод",
                AmountMinor = tx.AmountMinor,
                Payer = tx.FromId,
                Share = [new ShareEntry { ParticipantId = tx.ToId, AmountMinor = tx.AmountMinor }],
                SplitType = SplitType.ByAmounts,
                CreatedBy = userId,
                IsTransfer = true
            });
        }

        trip.Settlements.Clear();
        trip.Status = TripStatus.Active;
        trip.Version++;
        await tripRepo.SaveChangesAsync();

        var otherMemberIds = trip.Members
            .Select(m => m.UserId)
            .Where(id => id != userId)
            .ToList();

        if (otherMemberIds.Count > 0)
            await pushService.NotifyManyAsync(otherMemberIds, "Подсчёт переоткрыт",
                $"{trip.Name}: снова можно добавлять расходы");

        return await NotifyAndBuildAsync(trip);
    }

    public async Task<SettlementsResponse> SetPaidAsync(string tripId, string userId, string transactionId, bool paid)
    {
        var trip = await GetTripForMemberAsync(tripId, userId);

        if (trip.Status == TripStatus.Active)
            throw new DomainException("NOT_FINALIZED", "Подсчёт ещё не завершён");

        var tx = trip.Settlements.FirstOrDefault(s => s.Id == transactionId)
                 ?? throw new DomainException("TRANSACTION_NOT_FOUND", "Перевод не найден");

        // Отметить может любой из двух концов перевода; за гостя — любой участник.
        var canMark = tx.FromId == userId || tx.ToId == userId
                      || tx.FromId.StartsWith("g_") || tx.ToId.StartsWith("g_");
        if (!canMark)
            throw new DomainException("FORBIDDEN", "Отметить оплату может только участник этого перевода");

        if (tx.IsPaid == paid)
            return await BuildResponseAsync(trip);

        tx.IsPaid = paid;
        tx.PaidAt = paid ? DateTime.UtcNow : null;
        tx.PaidMarkedById = paid ? userId : null;

        var newStatus = trip.Settlements.All(s => s.IsPaid) ? TripStatus.Settled : TripStatus.Settling;
        var statusChanged = newStatus != trip.Status;
        trip.Status = newStatus;
        trip.Version++;
        await tripRepo.SaveChangesAsync();

        // Пуш обоим концам перевода (кроме того, кто нажал): должник узнаёт, что
        // кредитор подтвердил получение, кредитор — что должник оплатил.
        var endRecipients = new[] { tx.FromId, tx.ToId }
            .Where(id => id != userId && trip.Members.Any(m => m.UserId == id))
            .Distinct()
            .ToList();

        if (endRecipients.Count > 0)
            await pushService.NotifyManyAsync(endRecipients,
                paid ? "Перевод оплачен" : "Отметка об оплате снята",
                $"{trip.Name}: {ResolveName(trip, tx.FromId)} → {ResolveName(trip, tx.ToId)}, {tx.AmountMinor / 100m:F2} ₽");

        // Смена статуса поездки касается всех — остальным участникам отдельный пуш
        if (statusChanged)
        {
            var others = trip.Members
                .Select(m => m.UserId)
                .Where(id => id != userId && !endRecipients.Contains(id))
                .ToList();

            if (others.Count > 0)
                await pushService.NotifyManyAsync(others,
                    newStatus == TripStatus.Settled ? "Поездка закрыта" : "Поездка снова в расчёте",
                    newStatus == TripStatus.Settled
                        ? $"{trip.Name}: все переводы оплачены"
                        : $"{trip.Name}: отметка об оплате снята");
        }

        return await NotifyAndBuildAsync(trip);
    }

    private static string ResolveName(Trip trip, string participantId) =>
        participantId.StartsWith("g_")
            ? trip.Guests.FirstOrDefault(g => g.Id == participantId)?.DisplayName ?? "Гость"
            : trip.Members.FirstOrDefault(m => m.UserId == participantId)?.User.DisplayName ?? "Участник";

    private async Task<SettlementsResponse> NotifyAndBuildAsync(Trip trip)
    {
        var response = await BuildResponseAsync(trip);
        await notifier.TripUpdatedAsync(trip.Id, TripService.MapToDetail(trip));
        await notifier.SettlementUpdatedAsync(trip.Id, response);
        return response;
    }

    private async Task<Trip> GetTripForMemberAsync(string tripId, string userId)
    {
        var trip = await tripRepo.GetByIdWithDetailsAsync(tripId)
                   ?? throw new DomainException("NOT_FOUND", "Trip not found");

        var isMember = trip.Members.Any(m => m.UserId == userId);
        if (!isMember)
            throw new DomainException("FORBIDDEN", "You are not a member of this trip");

        return trip;
    }

    private async Task<SettlementsResponse> BuildResponseAsync(Trip trip)
    {
        var (balances, computed) = SettlementsCalculator.Compute(trip.Expenses);

        // Active — живой предварительный расчёт; иначе — зафиксированные транзакции
        var transactions = trip.Status == TripStatus.Active
            ? computed.Select(t => new SettlementDto(t.From, t.To, t.Amount)).ToList()
            : trip.Settlements
                .OrderByDescending(s => s.AmountMinor).ThenBy(s => s.FromId).ThenBy(s => s.Id)
                .Select(s => new SettlementDto(s.FromId, s.ToId, s.AmountMinor / 100m,
                    Id: s.Id, IsPaid: s.IsPaid, PaidAt: s.PaidAt))
                .ToList();

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

        transactions = transactions.Select(t =>
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

            return t with { ToPayment = toPayment };
        }).ToList();

        return new SettlementsResponse(trip.Status.ToDto(), balances, transactions);
    }
}
