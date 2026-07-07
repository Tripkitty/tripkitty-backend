using NSubstitute;
using Tripkitty.Application.DTOs;
using Tripkitty.Application.Logic;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Tests;

public class PhoneNormalizerTests
{
    [Theory]
    [InlineData("+7 999 123-45-67", "+79991234567")]
    [InlineData("89991234567", "+79991234567")]
    [InlineData("79991234567", "+79991234567")]
    [InlineData("9991234567", "+79991234567")]
    [InlineData("+7 (999) 123 45 67", "+79991234567")]
    public void Normalize_CanonicalizesRuNumbers(string input, string expected)
    {
        Assert.Equal(expected, PhoneNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("+1 202 555 0100")]      // не 10 цифр нацномера
    [InlineData("999123456")]            // 9 цифр
    [InlineData("999123456789")]         // слишком длинный
    public void Normalize_ThrowsInvalidPhone_ForBadInput(string? input)
    {
        var ex = Assert.Throws<DomainException>(() => PhoneNormalizer.Normalize(input));
        Assert.Equal("INVALID_PHONE", ex.Code);
    }
}

public class PaymentDetailsFactoryTests
{
    [Fact]
    public void FromRequest_ReturnsNull_ForNull()
    {
        Assert.Null(PaymentDetailsFactory.FromRequest(null));
    }

    [Fact]
    public void FromRequest_NormalizesPhoneAndDedupesBanks()
    {
        var info = PaymentDetailsFactory.FromRequest(
            new PaymentDetailsRequest("89991234567", new List<string> { "SBERBANK", "TBANK", "SBERBANK" }, "  Основной  "));

        Assert.NotNull(info);
        Assert.Equal("+79991234567", info!.Phone);
        Assert.Equal(new List<string> { "SBERBANK", "TBANK" }, info.Banks);
        Assert.Equal("Основной", info.Label);
    }

    [Fact]
    public void FromRequest_ThrowsInvalidBank_ForUnknownBank()
    {
        var ex = Assert.Throws<DomainException>(() =>
            PaymentDetailsFactory.FromRequest(new PaymentDetailsRequest("89991234567", new List<string> { "RANDOMBANK" })));
        Assert.Equal("INVALID_BANK", ex.Code);
    }

    [Fact]
    public void FromRequest_ThrowsValidation_ForEmptyBanks()
    {
        var ex = Assert.Throws<DomainException>(() =>
            PaymentDetailsFactory.FromRequest(new PaymentDetailsRequest("89991234567", new List<string>())));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }
}

public class PaymentMethodServiceTests
{
    private readonly IPaymentMethodRepository _repo = Substitute.For<IPaymentMethodRepository>();
    private readonly PaymentMethodService _sut;

    public PaymentMethodServiceTests()
    {
        _sut = new PaymentMethodService(_repo);
    }

    [Fact]
    public async Task Create_FirstMethod_BecomesDefault()
    {
        _repo.GetForUserAsync("u_1").Returns(new List<PaymentMethod>());

        var dto = await _sut.CreateAsync("u_1",
            new CreatePaymentMethodRequest("89991234567", new List<string> { "SBERBANK" }, IsDefault: false));

        Assert.True(dto.IsDefault);
        Assert.Equal("+79991234567", dto.Phone);
    }

    [Fact]
    public async Task Create_WithIsDefault_UnsetsPreviousDefault()
    {
        var existing = new PaymentMethod { Id = "pm_old", UserId = "u_1", IsDefault = true };
        _repo.GetForUserAsync("u_1").Returns(new List<PaymentMethod> { existing });

        var dto = await _sut.CreateAsync("u_1",
            new CreatePaymentMethodRequest("89991234567", new List<string> { "TBANK" }, IsDefault: true));

        Assert.True(dto.IsDefault);
        Assert.False(existing.IsDefault);
    }

    [Fact]
    public async Task Create_ThrowsValidation_WhenNoBanks()
    {
        _repo.GetForUserAsync("u_1").Returns(new List<PaymentMethod>());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.CreateAsync("u_1", new CreatePaymentMethodRequest("89991234567", new List<string>())));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }

    [Fact]
    public async Task Update_ThrowsNotFound_ForOtherUsersMethod()
    {
        _repo.FindByIdAsync("pm_1").Returns(new PaymentMethod { Id = "pm_1", UserId = "u_2" });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.UpdateAsync("u_1", "pm_1", new UpdatePaymentMethodRequest(null, null, null, null)));
        Assert.Equal("PAYMENT_METHOD_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task Delete_PromotesAnotherMethod_WhenDefaultRemoved()
    {
        var toDelete = new PaymentMethod { Id = "pm_1", UserId = "u_1", IsDefault = true };
        var other = new PaymentMethod { Id = "pm_2", UserId = "u_1", IsDefault = false };
        _repo.FindByIdAsync("pm_1").Returns(toDelete);
        _repo.GetForUserAsync("u_1").Returns(new List<PaymentMethod> { toDelete, other });

        await _sut.DeleteAsync("u_1", "pm_1");

        _repo.Received().Remove(toDelete);
        Assert.True(other.IsDefault);
    }
}

public class TripPaymentResolutionTests
{
    private readonly ITripRepository _tripRepo = Substitute.For<ITripRepository>();
    private readonly IFriendshipRepository _friendRepo = Substitute.For<IFriendshipRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IPushNotificationService _push = Substitute.For<IPushNotificationService>();
    private readonly ITripNotifier _notifier = Substitute.For<ITripNotifier>();
    private readonly IPaymentMethodRepository _pmRepo = Substitute.For<IPaymentMethodRepository>();
    private readonly ParticipantService _sut;

    public TripPaymentResolutionTests()
    {
        _sut = new ParticipantService(_tripRepo, _friendRepo, _userRepo, _push, _notifier, _pmRepo);
    }

    private Trip TripWith(TripMember member) =>
        new() { Id = "t1", Members = new List<TripMember> { member } };

    [Fact]
    public async Task GetMyPayment_UsesTripOverride_WhenSet()
    {
        var member = new TripMember
        {
            TripId = "t1",
            UserId = "u_1",
            PaymentDetails = new PaymentDetails { Phone = "+79991234567", Banks = new() { "TBANK" } }
        };
        _tripRepo.GetByIdWithDetailsAsync("t1").Returns(TripWith(member));

        var result = await _sut.GetMyPaymentAsync("t1", "u_1");

        Assert.Equal("trip", result.Source);
        Assert.Equal("+79991234567", result.Payment!.Phone);
        await _pmRepo.DidNotReceive().GetForUserAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetMyPayment_FallsBackToProfileDefault_WhenNoOverride()
    {
        var member = new TripMember { TripId = "t1", UserId = "u_1", PaymentDetails = null };
        _tripRepo.GetByIdWithDetailsAsync("t1").Returns(TripWith(member));
        _pmRepo.GetForUserAsync("u_1").Returns(new List<PaymentMethod>
        {
            new() { Id = "pm_1", UserId = "u_1", Phone = "+79990000000", Banks = new() { "SBERBANK" }, IsDefault = true }
        });

        var result = await _sut.GetMyPaymentAsync("t1", "u_1");

        Assert.Equal("profile", result.Source);
        Assert.Equal("+79990000000", result.Payment!.Phone);
    }

    [Fact]
    public async Task GetMyPayment_ReturnsNone_WhenNoOverrideAndNoProfile()
    {
        var member = new TripMember { TripId = "t1", UserId = "u_1", PaymentDetails = null };
        _tripRepo.GetByIdWithDetailsAsync("t1").Returns(TripWith(member));
        _pmRepo.GetForUserAsync("u_1").Returns(new List<PaymentMethod>());

        var result = await _sut.GetMyPaymentAsync("t1", "u_1");

        Assert.Equal("none", result.Source);
        Assert.Null(result.Payment);
    }

    [Fact]
    public async Task GetMyPayment_ThrowsForbidden_WhenNotMember()
    {
        _tripRepo.GetByIdWithDetailsAsync("t1").Returns(TripWith(new TripMember { TripId = "t1", UserId = "u_2" }));

        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.GetMyPaymentAsync("t1", "u_1"));
        Assert.Equal("FORBIDDEN", ex.Code);
    }
}
