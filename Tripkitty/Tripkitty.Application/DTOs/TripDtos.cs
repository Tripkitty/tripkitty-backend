namespace Tripkitty.Application.DTOs;

public record CreateTripRequest(string Name, string Cur = "RUB");

public record PatchTripRequest(string? Name, DateOnly? Start, DateOnly? End);

public record TripSummaryDto(string Id, string Name, string Cur, string OwnerId, DateOnly? Start, DateOnly? End, long Version);

public record MemberDto(string Id, string Name, string Handle, string Email);

public record GuestDto(string Id, string Name);

public record ExpenseDto(string Id, string Title, decimal Amount, string Payer, List<string> Share, string CreatedBy);

public record TripEventDto(string Id, string Title, string Date, string? Time, string? EndTime, string CreatedBy);

public record TripDetailDto(
    string Id,
    string Name,
    string Cur,
    string OwnerId,
    DateOnly? Start,
    DateOnly? End,
    long Version,
    List<MemberDto> Members,
    List<GuestDto> Guests,
    List<ExpenseDto> Expenses,
    List<TripEventDto> Events
);
