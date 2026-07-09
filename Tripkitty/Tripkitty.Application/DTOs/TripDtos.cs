using Tripkitty.Domain.Entities;

namespace Tripkitty.Application.DTOs;

public record CreateTripRequest(string Name, string Cur = "RUB");

public record PatchTripRequest(string? Name, DateOnly? Start, DateOnly? End);

public record TripSummaryDto(string Id, string Name, string Cur, string OwnerId, DateOnly? Start, DateOnly? End, long Version, string Status);

public static class TripStatusExtensions
{
    public static string ToDto(this TripStatus status) => status switch
    {
        TripStatus.Settling => "settling",
        TripStatus.Settled => "settled",
        _ => "active"
    };
}

public record MemberDto(string Id, string Name, string LastName, string FirstName, string? MiddleName, string Handle, string Email)
{
    public static MemberDto From(User u) => new(u.Id, u.DisplayName, u.LastName, u.FirstName, u.MiddleName, u.Handle, u.Email);
}

public record GuestDto(string Id, string Name, string LastName, string FirstName, string? MiddleName, PaymentDetailsDto? PaymentDetails)
{
    public static GuestDto From(Guest g) => new(g.Id, g.DisplayName, g.LastName, g.FirstName, g.MiddleName, PaymentDetailsDto.From(g.PaymentDetails));
    public static GuestDto From(User u) => new(u.Id, u.DisplayName, u.LastName, u.FirstName, u.MiddleName, null);
}

public record ExpenseDto(string Id, string Title, decimal Amount, string Payer, List<ShareEntryDto> Share, SplitType SplitType, string CreatedBy, bool IsTransfer = false);

public record TripEventDto(string Id, string Title, string Date, string? Time, string? EndTime, string CreatedBy);

public record TripDetailDto(
    string Id,
    string Name,
    string Cur,
    string OwnerId,
    DateOnly? Start,
    DateOnly? End,
    long Version,
    string Status,
    List<MemberDto> Members,
    List<GuestDto> Guests,
    List<ExpenseDto> Expenses,
    List<TripEventDto> Events
);
