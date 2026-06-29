using NSubstitute;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Tests;

public class FriendServiceTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IFriendshipRepository _friendRepo = Substitute.For<IFriendshipRepository>();
    private readonly IPushNotificationService _push = Substitute.For<IPushNotificationService>();
    private readonly FriendService _sut;

    public FriendServiceTests()
    {
        _sut = new FriendService(_userRepo, _friendRepo, _push);
    }

    // --- SearchByHandle ---

    [Fact]
    public async Task SearchByHandle_StripsPrefixAndLowercases()
    {
        _userRepo.FindByHandleAsync("anya").Returns(new User { Id = "u_1", Name = "Anya", Handle = "anya", Email = "a@b.com" });

        var result = await _sut.SearchByHandleAsync("@ANYA");

        Assert.NotNull(result);
        Assert.Equal("u_1", result!.Id);
    }

    [Fact]
    public async Task SearchByHandle_ReturnsNull_WhenNotFound()
    {
        _userRepo.FindByHandleAsync(Arg.Any<string>()).Returns((User?)null);

        var result = await _sut.SearchByHandleAsync("nobody");

        Assert.Null(result);
    }

    // --- SendRequest ---

    [Fact]
    public async Task SendRequest_ThrowsSelfRequest_WhenSendingToSelf()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.SendRequestAsync("u_1", new SendFriendRequestRequest(null, "u_1")));

        Assert.Equal("SELF_REQUEST", ex.Code);
    }

    [Fact]
    public async Task SendRequest_ThrowsValidationError_WhenNoHandleOrUserId()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.SendRequestAsync("u_1", new SendFriendRequestRequest(null, null)));

        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }

    [Fact]
    public async Task SendRequest_ThrowsAlreadyFriends_WhenFriendshipAccepted()
    {
        var friendship = new Friendship
        {
            UserAId = "u_1", UserBId = "u_2",
            RequestedById = "u_1",
            Status = FriendshipStatus.Accepted
        };
        _friendRepo.FindAsync("u_1", "u_2").Returns(friendship);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.SendRequestAsync("u_1", new SendFriendRequestRequest(null, "u_2")));

        Assert.Equal("ALREADY_FRIENDS", ex.Code);
    }

    [Fact]
    public async Task SendRequest_ThrowsRequestExists_WhenDuplicateOutgoing()
    {
        var friendship = new Friendship
        {
            UserAId = "u_1", UserBId = "u_2",
            RequestedById = "u_1",
            Status = FriendshipStatus.Pending
        };
        _friendRepo.FindAsync("u_1", "u_2").Returns(friendship);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.SendRequestAsync("u_1", new SendFriendRequestRequest(null, "u_2")));

        Assert.Equal("REQUEST_EXISTS", ex.Code);
    }

    [Fact]
    public async Task SendRequest_AutoAccepts_WhenCounterRequestExists()
    {
        // u_2 already sent request to u_1 => pending with RequestedById=u_2
        var friendship = new Friendship
        {
            UserAId = "u_1", UserBId = "u_2",
            RequestedById = "u_2",
            Status = FriendshipStatus.Pending
        };
        _friendRepo.FindAsync("u_1", "u_2").Returns(friendship);
        _userRepo.FindByIdAsync("u_1").Returns(new User { Id = "u_1", Name = "Alice" });

        await _sut.SendRequestAsync("u_1", new SendFriendRequestRequest(null, "u_2"));

        Assert.Equal(FriendshipStatus.Accepted, friendship.Status);
        await _friendRepo.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task SendRequest_CreatesNewFriendship_WhenNoExisting()
    {
        _friendRepo.FindAsync(Arg.Any<string>(), Arg.Any<string>()).Returns((Friendship?)null);
        _userRepo.FindByIdAsync("u_2").Returns(new User { Id = "u_2", Name = "Bob" });
        _userRepo.FindByIdAsync("u_1").Returns(new User { Id = "u_1", Name = "Alice" });

        await _sut.SendRequestAsync("u_1", new SendFriendRequestRequest(null, "u_2"));

        await _friendRepo.Received(1).AddAsync(Arg.Any<Friendship>());
        await _friendRepo.Received(1).SaveChangesAsync();
    }

    // --- Normalize: UserAId < UserBId ---

    [Fact]
    public async Task SendRequest_NormalizesIds_SmallestFirst()
    {
        _friendRepo.FindAsync(Arg.Any<string>(), Arg.Any<string>()).Returns((Friendship?)null);
        _userRepo.FindByIdAsync(Arg.Any<string>()).Returns(new User { Id = "zzz", Name = "Z" });

        await _sut.SendRequestAsync("zzz", new SendFriendRequestRequest(null, "aaa"));

        await _friendRepo.Received(1).AddAsync(Arg.Is<Friendship>(f =>
            string.Compare(f.UserAId, f.UserBId, StringComparison.Ordinal) < 0));
    }

    // --- Accept ---

    [Fact]
    public async Task Accept_ThrowsForbidden_WhenAcceptingOwnRequest()
    {
        var friendship = new Friendship
        {
            UserAId = "u_1", UserBId = "u_2",
            RequestedById = "u_1",
            Status = FriendshipStatus.Pending
        };
        _friendRepo.FindAsync("u_1", "u_2").Returns(friendship);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.AcceptAsync("u_1", "u_2"));

        Assert.Equal("FORBIDDEN", ex.Code);
    }

    [Fact]
    public async Task Accept_SetsStatusAccepted()
    {
        var friendship = new Friendship
        {
            UserAId = "u_1", UserBId = "u_2",
            RequestedById = "u_2",
            Status = FriendshipStatus.Pending
        };
        _friendRepo.FindAsync("u_1", "u_2").Returns(friendship);
        _userRepo.FindByIdAsync("u_1").Returns(new User { Id = "u_1", Name = "Alice" });

        await _sut.AcceptAsync("u_1", "u_2");

        Assert.Equal(FriendshipStatus.Accepted, friendship.Status);
        await _friendRepo.Received(1).SaveChangesAsync();
    }

    // --- Decline ---

    [Fact]
    public async Task Decline_RemovesFriendship()
    {
        var friendship = new Friendship
        {
            UserAId = "u_1", UserBId = "u_2",
            RequestedById = "u_2",
            Status = FriendshipStatus.Pending
        };
        _friendRepo.FindAsync("u_1", "u_2").Returns(friendship);

        await _sut.DeclineAsync("u_1", "u_2");

        await _friendRepo.Received(1).RemoveAsync(friendship);
        await _friendRepo.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Decline_ThrowsAlreadyFriends_WhenAccepted()
    {
        var friendship = new Friendship
        {
            UserAId = "u_1", UserBId = "u_2",
            RequestedById = "u_2",
            Status = FriendshipStatus.Accepted
        };
        _friendRepo.FindAsync("u_1", "u_2").Returns(friendship);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.DeclineAsync("u_1", "u_2"));

        Assert.Equal("ALREADY_FRIENDS", ex.Code);
    }

    // --- GetFriends ---

    [Fact]
    public async Task GetFriends_CorrectlyCategorizes_IncomingOutgoingAccepted()
    {
        var alice = new User { Id = "u_alice", Name = "Alice", Handle = "alice", Email = "a@b.com" };
        var bob   = new User { Id = "u_bob",   Name = "Bob",   Handle = "bob",   Email = "b@b.com" };
        var carol = new User { Id = "u_carol", Name = "Carol", Handle = "carol", Email = "c@b.com" };
        var dave  = new User { Id = "u_dave",  Name = "Dave",  Handle = "dave",  Email = "d@b.com" };

        // alice <-> bob: accepted
        var f1 = new Friendship { UserAId = "u_alice", UserBId = "u_bob", RequestedById = "u_alice", Status = FriendshipStatus.Accepted, UserA = alice, UserB = bob };
        // carol -> alice: pending incoming for alice
        var f2 = new Friendship { UserAId = "u_alice", UserBId = "u_carol", RequestedById = "u_carol", Status = FriendshipStatus.Pending, UserA = alice, UserB = carol };
        // alice -> dave: pending outgoing for alice
        var f3 = new Friendship { UserAId = "u_alice", UserBId = "u_dave", RequestedById = "u_alice", Status = FriendshipStatus.Pending, UserA = alice, UserB = dave };

        _friendRepo.GetAllForUserAsync("u_alice").Returns([f1, f2, f3]);

        var result = await _sut.GetFriendsAsync("u_alice");

        Assert.Single(result.Friends);
        Assert.Equal("u_bob", result.Friends[0].Id);

        Assert.Single(result.Incoming);
        Assert.Equal("u_carol", result.Incoming[0].Id);

        Assert.Single(result.Outgoing);
        Assert.Equal("u_dave", result.Outgoing[0].Id);
    }
}
