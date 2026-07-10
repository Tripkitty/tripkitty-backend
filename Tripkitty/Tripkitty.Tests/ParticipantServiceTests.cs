using NSubstitute;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Tests;

public class ParticipantServiceTests
{
    private readonly ITripRepository _tripRepo = Substitute.For<ITripRepository>();
    private readonly IFriendshipRepository _friendRepo = Substitute.For<IFriendshipRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IPushNotificationService _push = Substitute.For<IPushNotificationService>();
    private readonly ITripNotifier _notifier = Substitute.For<ITripNotifier>();
    private readonly IPaymentMethodRepository _pmRepo = Substitute.For<IPaymentMethodRepository>();
    private readonly ParticipantService _sut;

    public ParticipantServiceTests()
    {
        _sut = new ParticipantService(_tripRepo, _friendRepo, _userRepo, _push, _notifier, _pmRepo);
    }

    private Trip MakeTrip(params string[] memberIds)
    {
        var trip = new Trip { Id = "t1", Name = "Тест", OwnerId = memberIds[0] };
        foreach (var id in memberIds)
            trip.Members.Add(new TripMember { TripId = "t1", UserId = id, User = new User { Id = id } });
        _tripRepo.GetByIdWithDetailsAsync("t1").Returns(trip);
        return trip;
    }

    private static Guest AddGuest(Trip trip, string id)
    {
        var guest = new Guest { Id = id, TripId = trip.Id, FirstName = "Гость", LastName = "Тестовый" };
        trip.Guests.Add(guest);
        return guest;
    }

    [Fact]
    public async Task SetSponsor_AssignsSelfForGuest_AndNotifies()
    {
        var trip = MakeTrip("u_1", "u_2");
        var guest = AddGuest(trip, "g_1");

        var detail = await _sut.SetSponsorAsync("t1", "u_1", "g_1", "u_1");

        Assert.Equal("u_1", guest.SponsorId);
        Assert.Equal(2, trip.Version);
        Assert.Equal("u_1", detail.Guests.Single().SponsorId);
        await _notifier.Received(1).TripUpdatedAsync("t1", Arg.Any<TripDetailDto>());
    }

    [Fact]
    public async Task SetSponsor_AssignsSelfForMember()
    {
        var trip = MakeTrip("u_1", "u_2");

        var detail = await _sut.SetSponsorAsync("t1", "u_1", "u_2", "u_1");

        Assert.Equal("u_1", trip.Members.Single(m => m.UserId == "u_2").SponsorId);
        Assert.Equal("u_1", detail.Members.Single(m => m.Id == "u_2").SponsorId);
    }

    [Fact]
    public async Task SetSponsor_ThrowsNotSponsor_WhenAssigningSomeoneElse()
    {
        var trip = MakeTrip("u_1", "u_2");
        AddGuest(trip, "g_1");

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.SetSponsorAsync("t1", "u_1", "g_1", "u_2"));
        Assert.Equal("NOT_SPONSOR", ex.Code);
    }

    [Fact]
    public async Task SetSponsor_ThrowsSponsorSelf_WhenTargetIsCaller()
    {
        MakeTrip("u_1", "u_2");

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.SetSponsorAsync("t1", "u_1", "u_1", "u_1"));
        Assert.Equal("SPONSOR_SELF", ex.Code);
    }

    [Fact]
    public async Task SetSponsor_ThrowsSponsorChain_WhenCallerHasSponsor()
    {
        var trip = MakeTrip("u_1", "u_2");
        trip.Members.Single(m => m.UserId == "u_1").SponsorId = "u_2";
        AddGuest(trip, "g_1");

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.SetSponsorAsync("t1", "u_1", "g_1", "u_1"));
        Assert.Equal("SPONSOR_CHAIN", ex.Code);
    }

    [Fact]
    public async Task SetSponsor_ThrowsSponsorChain_WhenTargetSponsorsOthers()
    {
        var trip = MakeTrip("u_1", "u_2");
        AddGuest(trip, "g_1").SponsorId = "u_2";

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.SetSponsorAsync("t1", "u_1", "u_2", "u_1"));
        Assert.Equal("SPONSOR_CHAIN", ex.Code);
    }

    [Fact]
    public async Task SetSponsor_ThrowsSponsorTaken_WhenAnotherSponsorExists()
    {
        var trip = MakeTrip("u_1", "u_2");
        AddGuest(trip, "g_1").SponsorId = "u_2";

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.SetSponsorAsync("t1", "u_1", "g_1", "u_1"));
        Assert.Equal("SPONSOR_TAKEN", ex.Code);
    }

    [Fact]
    public async Task SetSponsor_Clear_ThrowsNotSponsor_WhenCallerIsNotCurrentSponsor()
    {
        var trip = MakeTrip("u_1", "u_2");
        AddGuest(trip, "g_1").SponsorId = "u_2";

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.SetSponsorAsync("t1", "u_1", "g_1", null));
        Assert.Equal("NOT_SPONSOR", ex.Code);
    }

    [Fact]
    public async Task SetSponsor_Clear_ByCurrentSponsor_Resets()
    {
        var trip = MakeTrip("u_1", "u_2");
        var guest = AddGuest(trip, "g_1");
        guest.SponsorId = "u_2";

        await _sut.SetSponsorAsync("t1", "u_2", "g_1", null);

        Assert.Null(guest.SponsorId);
        Assert.Equal(2, trip.Version);
    }

    [Fact]
    public async Task SetSponsor_ThrowsTripSettling_WhenNotActive()
    {
        var trip = MakeTrip("u_1", "u_2");
        AddGuest(trip, "g_1");
        trip.Status = TripStatus.Settling;

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.SetSponsorAsync("t1", "u_1", "g_1", "u_1"));
        Assert.Equal("TRIP_SETTLING", ex.Code);
    }

    [Fact]
    public async Task RemoveParticipant_ThrowsParticipantIsSponsor_WhenDependentsExist()
    {
        var trip = MakeTrip("u_1", "u_2");
        AddGuest(trip, "g_1").SponsorId = "u_2";

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.RemoveParticipantAsync("t1", "u_1", "u_2"));
        Assert.Equal("PARTICIPANT_IS_SPONSOR", ex.Code);
        Assert.NotNull(ex.Details);
    }

    [Fact]
    public async Task RemoveParticipant_AllowsRemovingDependent()
    {
        var trip = MakeTrip("u_1", "u_2");
        var guest = AddGuest(trip, "g_1");
        guest.SponsorId = "u_2";

        await _sut.RemoveParticipantAsync("t1", "u_1", "g_1");

        Assert.Empty(trip.Guests);
    }
}
