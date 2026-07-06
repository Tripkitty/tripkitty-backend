namespace Tripkitty.Application.DTOs;

public record AddMemberRequest(string UserId);

public record AddGuestRequest(string LastName, string FirstName, string? MiddleName);
