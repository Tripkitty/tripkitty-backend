namespace Tripkitty.Application.DTOs;

public record RegisterRequest(string Name, string Handle, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record UserDto(string Id, string Name, string Handle, string Email);

public record TokensDto(string AccessToken, string RefreshToken);

public record AuthResponse(UserDto User, TokensDto Tokens);
