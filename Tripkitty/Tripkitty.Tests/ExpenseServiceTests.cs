using NSubstitute;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Tests;

public class ExpenseServiceTests
{
    private readonly ITripRepository _tripRepo = Substitute.For<ITripRepository>();
    private readonly IPushNotificationService _push = Substitute.For<IPushNotificationService>();
    private readonly ITripNotifier _notifier = Substitute.For<ITripNotifier>();
    private readonly ExpenseService _sut;

    public ExpenseServiceTests()
    {
        _sut = new ExpenseService(_tripRepo, _push, _notifier);
    }

    private Trip MakeTrip(params string[] memberIds)
    {
        var trip = new Trip { Id = "t1", Name = "Тест", OwnerId = memberIds[0] };
        foreach (var id in memberIds)
            trip.Members.Add(new TripMember { TripId = "t1", UserId = id, User = new User { Id = id } });
        _tripRepo.GetByIdWithDetailsAsync("t1").Returns(trip);
        return trip;
    }

    private static AddExpenseRequest Request(string payer, decimal amount, string[] share,
        Dictionary<string, string>? sponsors = null) =>
        new("Ужин", amount, payer,
            share.Select(id => new ShareEntryRequest(id)).ToList(),
            Sponsors: sponsors);

    [Fact]
    public async Task Add_SnapshotsLiveSponsorship_WhenSponsorsNotSent()
    {
        var trip = MakeTrip("u_1", "u_2", "u_3");
        trip.Members.Single(m => m.UserId == "u_2").SponsorId = "u_1";

        var dto = await _sut.AddAsync("t1", "u_1", Request("u_1", 300m, ["u_1", "u_2", "u_3"]));

        var expense = Assert.Single(trip.Expenses);
        Assert.Equal("u_1", expense.Sponsors["u_2"]);
        Assert.Equal("u_1", dto.Sponsors!["u_2"]);
    }

    [Fact]
    public async Task Add_ExplicitEmptySponsors_OverridesLiveDefault()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Members.Single(m => m.UserId == "u_2").SponsorId = "u_1";

        await _sut.AddAsync("t1", "u_1", Request("u_1", 100m, ["u_2"], sponsors: new()));

        Assert.Empty(Assert.Single(trip.Expenses).Sponsors);
    }

    [Fact]
    public async Task Add_ThrowsInvalidSponsors_WhenPairNotLive()
    {
        MakeTrip("u_1", "u_2");

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.AddAsync("t1", "u_1", Request("u_1", 100m, ["u_2"],
                sponsors: new() { ["u_2"] = "u_1" })));

        Assert.Equal("INVALID_SPONSORS", ex.Code);
    }

    [Fact]
    public async Task Update_KeepsSnapshot_WhenSponsorsNotSent_EvenAfterLiveSponsorshipRemoved()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Expenses.Add(new Expense
        {
            Id = "e1", TripId = "t1", Title = "Ужин", AmountMinor = 10000,
            Payer = "u_1", Share = [new ShareEntry { ParticipantId = "u_2" }],
            Sponsors = new Dictionary<string, string> { ["u_2"] = "u_1" }
        });
        // живое спонсорство уже снято — SponsorId ни у кого не стоит

        var (dto, _) = await _sut.UpdateAsync("t1", "u_1", "e1", Request("u_1", 150m, ["u_2"]));

        Assert.Equal("u_1", dto.Sponsors!["u_2"]);
    }

    [Fact]
    public async Task Update_CanReAddPairFromExpenseSnapshot_AfterLiveSponsorshipRemoved()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Expenses.Add(new Expense
        {
            Id = "e1", TripId = "t1", Title = "Ужин", AmountMinor = 10000,
            Payer = "u_1", Share = [new ShareEntry { ParticipantId = "u_2" }],
            Sponsors = new Dictionary<string, string> { ["u_2"] = "u_1" }
        });

        // пара живёт на расходе — её можно прислать заново, даже без живого спонсорства
        var (dto, _) = await _sut.UpdateAsync("t1", "u_1", "e1",
            Request("u_1", 150m, ["u_2"], sponsors: new() { ["u_2"] = "u_1" }));

        Assert.Equal("u_1", dto.Sponsors!["u_2"]);
    }

    [Fact]
    public async Task Update_EmptySponsors_ClearsSnapshot()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Expenses.Add(new Expense
        {
            Id = "e1", TripId = "t1", Title = "Шопинг", AmountMinor = 10000,
            Payer = "u_1", Share = [new ShareEntry { ParticipantId = "u_2" }],
            Sponsors = new Dictionary<string, string> { ["u_2"] = "u_1" }
        });

        var (dto, _) = await _sut.UpdateAsync("t1", "u_1", "e1",
            Request("u_1", 100m, ["u_2"], sponsors: new()));

        Assert.Empty(trip.Expenses.Single().Sponsors);
        Assert.Empty(dto.Sponsors!);
    }

    [Fact]
    public async Task Update_ThrowsInvalidSponsors_WhenPairNeitherLiveNorOnExpense()
    {
        var trip = MakeTrip("u_1", "u_2", "u_3");
        trip.Expenses.Add(new Expense
        {
            Id = "e1", TripId = "t1", Title = "Ужин", AmountMinor = 10000,
            Payer = "u_1", Share = [new ShareEntry { ParticipantId = "u_2" }]
        });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.UpdateAsync("t1", "u_1", "e1", Request("u_1", 100m, ["u_2"],
                sponsors: new() { ["u_2"] = "u_3" })));

        Assert.Equal("INVALID_SPONSORS", ex.Code);
    }
}
