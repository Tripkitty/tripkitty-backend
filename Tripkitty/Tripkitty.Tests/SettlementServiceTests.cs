using NSubstitute;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Logic;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Tests;

public class SettlementServiceTests
{
    private readonly ITripRepository _tripRepo = Substitute.For<ITripRepository>();
    private readonly IPushNotificationService _push = Substitute.For<IPushNotificationService>();
    private readonly ITripNotifier _notifier = Substitute.For<ITripNotifier>();
    private readonly IPaymentMethodRepository _pmRepo = Substitute.For<IPaymentMethodRepository>();
    private readonly SettlementService _sut;

    public SettlementServiceTests()
    {
        _pmRepo.GetForUsersAsync(Arg.Any<List<string>>()).Returns(new List<PaymentMethod>());
        _sut = new SettlementService(_tripRepo, _push, _notifier, _pmRepo);
    }

    private Trip MakeTrip(params string[] memberIds)
    {
        var trip = new Trip { Id = "t1", Name = "Тест", OwnerId = memberIds[0] };
        foreach (var id in memberIds)
            trip.Members.Add(new TripMember { TripId = "t1", UserId = id, User = new User { Id = id } });
        _tripRepo.GetByIdWithDetailsAsync("t1").Returns(trip);
        return trip;
    }

    private static Expense EqualExpense(string payer, decimal amount, params string[] share) =>
        new()
        {
            TripId = "t1",
            Title = "Ужин",
            AmountMinor = (long)(amount * 100),
            Payer = payer,
            Share = share.Select(id => new ShareEntry { ParticipantId = id }).ToList(),
            SplitType = SplitType.Equal,
            CreatedBy = payer
        };

    [Fact]
    public async Task Finalize_PersistsTransactions_AndSetsSettling()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Expenses.Add(EqualExpense("u_1", 100m, "u_1", "u_2"));

        var response = await _sut.FinalizeAsync("t1", "u_1");

        Assert.Equal("settling", response.Status);
        var tx = Assert.Single(trip.Settlements);
        Assert.Equal("u_2", tx.FromId);
        Assert.Equal("u_1", tx.ToId);
        Assert.Equal(5000, tx.AmountMinor);
        Assert.False(tx.IsPaid);
        Assert.Equal(2, trip.Version);

        var dto = Assert.Single(response.Transactions);
        Assert.Equal(tx.Id, dto.Id);
        Assert.False(dto.IsPaid);
        await _notifier.Received(1).SettlementUpdatedAsync("t1", Arg.Any<SettlementsResponse>());
    }

    [Fact]
    public async Task Finalize_SetsSettled_WhenNothingToTransfer()
    {
        var trip = MakeTrip("u_1", "u_2");

        var response = await _sut.FinalizeAsync("t1", "u_1");

        Assert.Equal("settled", response.Status);
        Assert.Empty(trip.Settlements);
    }

    [Fact]
    public async Task Finalize_ThrowsForbidden_WhenNotOwner()
    {
        MakeTrip("u_1", "u_2");

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.FinalizeAsync("t1", "u_2"));
        Assert.Equal("FORBIDDEN", ex.Code);
    }

    [Fact]
    public async Task Finalize_ThrowsAlreadyFinalized_WhenNotActive()
    {
        var trip = MakeTrip("u_1");
        trip.Status = TripStatus.Settling;

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.FinalizeAsync("t1", "u_1"));
        Assert.Equal("ALREADY_FINALIZED", ex.Code);
    }

    [Fact]
    public async Task SetPaid_MarksTransaction_AndTransitionsToSettled()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Status = TripStatus.Settling;
        var tx = new SettlementTransaction { TripId = "t1", FromId = "u_2", ToId = "u_1", AmountMinor = 5000 };
        trip.Settlements.Add(tx);

        var response = await _sut.SetPaidAsync("t1", "u_2", tx.Id, paid: true);

        Assert.True(tx.IsPaid);
        Assert.NotNull(tx.PaidAt);
        Assert.Equal("u_2", tx.PaidMarkedById);
        Assert.Equal("settled", response.Status);
        Assert.Equal(TripStatus.Settled, trip.Status);
    }

    [Fact]
    public async Task SetPaid_Unmark_RevertsToSettling()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Status = TripStatus.Settled;
        var tx = new SettlementTransaction
        {
            TripId = "t1", FromId = "u_2", ToId = "u_1", AmountMinor = 5000,
            IsPaid = true, PaidAt = DateTime.UtcNow, PaidMarkedById = "u_2"
        };
        trip.Settlements.Add(tx);

        var response = await _sut.SetPaidAsync("t1", "u_1", tx.Id, paid: false);

        Assert.False(tx.IsPaid);
        Assert.Null(tx.PaidAt);
        Assert.Null(tx.PaidMarkedById);
        Assert.Equal("settling", response.Status);
    }

    [Fact]
    public async Task SetPaid_NotifiesCreditor_WhenDebtorMarks()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Status = TripStatus.Settling;
        var tx = new SettlementTransaction { TripId = "t1", FromId = "u_2", ToId = "u_1", AmountMinor = 5000 };
        trip.Settlements.Add(tx);

        await _sut.SetPaidAsync("t1", "u_2", tx.Id, true);

        await _push.Received(1).NotifyManyAsync(
            Arg.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "u_1" })),
            "Перевод оплачен", Arg.Any<string>());
    }

    [Fact]
    public async Task SetPaid_NotifiesDebtor_WhenCreditorMarks()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Status = TripStatus.Settling;
        var tx = new SettlementTransaction { TripId = "t1", FromId = "u_2", ToId = "u_1", AmountMinor = 5000 };
        trip.Settlements.Add(tx);

        await _sut.SetPaidAsync("t1", "u_1", tx.Id, true);

        await _push.Received(1).NotifyManyAsync(
            Arg.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "u_2" })),
            "Перевод оплачен", Arg.Any<string>());
    }

    [Fact]
    public async Task SetPaid_PushesTripClosed_ToUninvolvedMembers_OnLastPayment()
    {
        var trip = MakeTrip("u_1", "u_2", "u_3");
        trip.Status = TripStatus.Settling;
        var tx = new SettlementTransaction { TripId = "t1", FromId = "u_2", ToId = "u_1", AmountMinor = 5000 };
        trip.Settlements.Add(tx);

        await _sut.SetPaidAsync("t1", "u_2", tx.Id, true);

        // u_1 — конец перевода, получает пуш об оплате; u_3 — пуш о закрытии поездки
        await _push.Received(1).NotifyManyAsync(
            Arg.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "u_3" })),
            "Поездка закрыта", Arg.Any<string>());
    }

    [Fact]
    public async Task SetPaid_NoStatusPush_WhileTransactionsRemain()
    {
        var trip = MakeTrip("u_1", "u_2", "u_3");
        trip.Status = TripStatus.Settling;
        var tx = new SettlementTransaction { TripId = "t1", FromId = "u_2", ToId = "u_1", AmountMinor = 5000 };
        trip.Settlements.Add(tx);
        trip.Settlements.Add(new SettlementTransaction { TripId = "t1", FromId = "u_3", ToId = "u_1", AmountMinor = 3000 });

        await _sut.SetPaidAsync("t1", "u_2", tx.Id, true);

        Assert.Equal(TripStatus.Settling, trip.Status);
        await _push.DidNotReceive().NotifyManyAsync(Arg.Any<IEnumerable<string>>(), "Поездка закрыта", Arg.Any<string>());
    }

    [Fact]
    public async Task SetPaid_PushesReopenedStatus_WhenUnmarkingFromSettled()
    {
        var trip = MakeTrip("u_1", "u_2", "u_3");
        trip.Status = TripStatus.Settled;
        var tx = new SettlementTransaction
        {
            TripId = "t1", FromId = "u_2", ToId = "u_1", AmountMinor = 5000,
            IsPaid = true, PaidAt = DateTime.UtcNow, PaidMarkedById = "u_2"
        };
        trip.Settlements.Add(tx);

        await _sut.SetPaidAsync("t1", "u_2", tx.Id, false);

        await _push.Received(1).NotifyManyAsync(
            Arg.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "u_3" })),
            "Поездка снова в расчёте", Arg.Any<string>());
    }

    [Fact]
    public async Task SetPaid_SendsSignalRSync()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Status = TripStatus.Settling;
        var tx = new SettlementTransaction { TripId = "t1", FromId = "u_2", ToId = "u_1", AmountMinor = 5000 };
        trip.Settlements.Add(tx);

        await _sut.SetPaidAsync("t1", "u_2", tx.Id, true);

        await _notifier.Received(1).TripUpdatedAsync("t1", Arg.Is<TripDetailDto>(t => t.Status == "settled"));
        await _notifier.Received(1).SettlementUpdatedAsync("t1",
            Arg.Is<SettlementsResponse>(r => r.Status == "settled" && r.Transactions[0].IsPaid == true));
    }

    [Fact]
    public async Task SetPaid_ThrowsForbidden_ForThirdPartyUser()
    {
        var trip = MakeTrip("u_1", "u_2", "u_3");
        trip.Status = TripStatus.Settling;
        var tx = new SettlementTransaction { TripId = "t1", FromId = "u_2", ToId = "u_1", AmountMinor = 5000 };
        trip.Settlements.Add(tx);

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.SetPaidAsync("t1", "u_3", tx.Id, true));
        Assert.Equal("FORBIDDEN", ex.Code);
    }

    [Fact]
    public async Task SetPaid_AllowsAnyMember_WhenEndIsGuest()
    {
        var trip = MakeTrip("u_1", "u_3");
        trip.Status = TripStatus.Settling;
        var tx = new SettlementTransaction { TripId = "t1", FromId = "g_1", ToId = "u_1", AmountMinor = 5000 };
        trip.Settlements.Add(tx);

        var response = await _sut.SetPaidAsync("t1", "u_3", tx.Id, true);

        Assert.True(tx.IsPaid);
        Assert.Equal("settled", response.Status);
    }

    [Fact]
    public async Task SetPaid_ThrowsNotFinalized_WhenActive()
    {
        MakeTrip("u_1");

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.SetPaidAsync("t1", "u_1", "tx1", true));
        Assert.Equal("NOT_FINALIZED", ex.Code);
    }

    [Fact]
    public async Task SetPaid_ThrowsNotFound_ForUnknownTransaction()
    {
        var trip = MakeTrip("u_1");
        trip.Status = TripStatus.Settling;

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.SetPaidAsync("t1", "u_1", "missing", true));
        Assert.Equal("TRANSACTION_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task Reopen_ConvertsPaidTransactionsToTransferExpenses()
    {
        var trip = MakeTrip("u_1", "u_2", "u_3");
        trip.Status = TripStatus.Settling;
        trip.Settlements.Add(new SettlementTransaction
        {
            TripId = "t1", FromId = "u_2", ToId = "u_1", AmountMinor = 5000, IsPaid = true
        });
        trip.Settlements.Add(new SettlementTransaction
        {
            TripId = "t1", FromId = "u_3", ToId = "u_1", AmountMinor = 3000, IsPaid = false
        });

        var response = await _sut.ReopenAsync("t1", "u_1");

        Assert.Equal("active", response.Status);
        Assert.Equal(TripStatus.Active, trip.Status);
        Assert.Empty(trip.Settlements);

        var transfer = Assert.Single(trip.Expenses);
        Assert.True(transfer.IsTransfer);
        Assert.Equal("u_2", transfer.Payer);
        Assert.Equal(5000, transfer.AmountMinor);
        Assert.Equal(SplitType.ByAmounts, transfer.SplitType);
        var share = Assert.Single(transfer.Share);
        Assert.Equal("u_1", share.ParticipantId);
        Assert.Equal(5000, share.AmountMinor);
    }

    [Fact]
    public async Task Reopen_ThrowsNotFinalized_WhenActive()
    {
        MakeTrip("u_1");

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.ReopenAsync("t1", "u_1"));
        Assert.Equal("NOT_FINALIZED", ex.Code);
    }

    [Fact]
    public async Task Reopen_ThrowsForbidden_WhenNotOwner()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Status = TripStatus.Settling;

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.ReopenAsync("t1", "u_2"));
        Assert.Equal("FORBIDDEN", ex.Code);
    }

    [Fact]
    public async Task Get_ReturnsStoredTransactions_WhenSettling()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Status = TripStatus.Settling;
        trip.Expenses.Add(EqualExpense("u_1", 100m, "u_1", "u_2"));
        var tx = new SettlementTransaction { TripId = "t1", FromId = "u_2", ToId = "u_1", AmountMinor = 5000 };
        trip.Settlements.Add(tx);

        var response = await _sut.GetAsync("t1", "u_2");

        Assert.Equal("settling", response.Status);
        var dto = Assert.Single(response.Transactions);
        Assert.Equal(tx.Id, dto.Id);
        Assert.Equal(50m, dto.Amount);
        Assert.False(dto.IsPaid);
    }

    [Fact]
    public async Task Get_ReturnsLiveComputation_WhenActive()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Expenses.Add(EqualExpense("u_1", 100m, "u_1", "u_2"));

        var response = await _sut.GetAsync("t1", "u_2");

        Assert.Equal("active", response.Status);
        var dto = Assert.Single(response.Transactions);
        Assert.Null(dto.Id);
        Assert.Null(dto.IsPaid);
        Assert.Equal("u_2", dto.From);
        Assert.Equal(50m, dto.Amount);
    }
}

public class SettlementGuardTests
{
    private readonly ITripRepository _tripRepo = Substitute.For<ITripRepository>();
    private readonly IPushNotificationService _push = Substitute.For<IPushNotificationService>();
    private readonly ITripNotifier _notifier = Substitute.For<ITripNotifier>();
    private readonly ExpenseService _sut;

    public SettlementGuardTests()
    {
        _sut = new ExpenseService(_tripRepo, _push, _notifier);
    }

    private Trip MakeSettlingTrip()
    {
        var trip = new Trip { Id = "t1", OwnerId = "u_1", Status = TripStatus.Settling };
        trip.Members.Add(new TripMember { TripId = "t1", UserId = "u_1", User = new User { Id = "u_1" } });
        _tripRepo.GetByIdWithDetailsAsync("t1").Returns(trip);
        return trip;
    }

    [Fact]
    public async Task AddExpense_ThrowsTripSettling_WhenFinalized()
    {
        MakeSettlingTrip();

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.AddAsync("t1", "u_1",
            new AddExpenseRequest("Ужин", 100m, "u_1", [new ShareEntryRequest("u_1")])));
        Assert.Equal("TRIP_SETTLING", ex.Code);
    }

    [Fact]
    public async Task RemoveExpense_ThrowsTransferReadonly_ForTransfer()
    {
        var trip = MakeSettlingTrip();
        trip.Status = TripStatus.Active;
        var transfer = new Expense { TripId = "t1", IsTransfer = true, Payer = "u_2" };
        trip.Expenses.Add(transfer);

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.RemoveAsync("t1", "u_1", transfer.Id));
        Assert.Equal("TRANSFER_READONLY", ex.Code);
    }
}

public class SettlementsCalculatorTransferTests
{
    [Fact]
    public void Compute_TransferExpense_SettlesDebt()
    {
        // u_1 заплатил 100 за двоих → u_2 должен 50; перевод u_2 → u_1 на 50 гасит долг
        var expenses = new List<Expense>
        {
            new()
            {
                Payer = "u_1", AmountMinor = 10000, SplitType = SplitType.Equal,
                Share = [new ShareEntry { ParticipantId = "u_1" }, new ShareEntry { ParticipantId = "u_2" }]
            },
            new()
            {
                Payer = "u_2", AmountMinor = 5000, SplitType = SplitType.ByAmounts, IsTransfer = true,
                Share = [new ShareEntry { ParticipantId = "u_1", AmountMinor = 5000 }]
            }
        };

        var (balances, transactions) = SettlementsCalculator.Compute(expenses);

        Assert.Empty(transactions);
        Assert.Equal(0m, balances["u_1"]);
        Assert.Equal(0m, balances["u_2"]);
    }
}
