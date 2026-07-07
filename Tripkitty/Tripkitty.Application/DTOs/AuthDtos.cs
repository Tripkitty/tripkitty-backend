using Tripkitty.Domain.Entities;

namespace Tripkitty.Application.DTOs;

public record RegisterRequest(string LastName, string FirstName, string? MiddleName, string Handle, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

// Частичное обновление ФИО профиля. null = не менять; для MiddleName пустая строка = сбросить.
public record UpdateProfileRequest(string? LastName, string? FirstName, string? MiddleName);

public record UserDto(string Id, string Name, string LastName, string FirstName, string? MiddleName, string Handle, string Email)
{
    public static UserDto From(User u) => new(u.Id, u.DisplayName, u.LastName, u.FirstName, u.MiddleName, u.Handle, u.Email);
}

public record TokensDto(string AccessToken, string RefreshToken);

public record AuthResponse(UserDto User, TokensDto Tokens);
