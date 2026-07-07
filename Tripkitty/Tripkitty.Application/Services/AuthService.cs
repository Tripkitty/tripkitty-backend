using System.Text.RegularExpressions;
using Tripkitty.Application.DTOs;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<UserDto> GetMeAsync(string userId);
    Task<UserDto> UpdateProfileAsync(string userId, UpdateProfileRequest request);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IJwtService
{
    string GenerateAccessToken(User user);
    Task<string> GenerateRefreshTokenAsync(string userId);
    Task<User?> ValidateRefreshTokenAsync(string rawToken);
    Task RevokeRefreshTokenAsync(string rawToken);
    Task<(User user, string newRawToken)> RotateRefreshTokenAsync(string rawToken);
}

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email);
    Task<User?> FindByHandleAsync(string handle);
    Task<User?> FindByIdAsync(string id);
    Task AddAsync(User user);
    Task SaveChangesAsync();
}

public class AuthService(
    IUserRepository userRepo,
    IPasswordHasher passwordHasher,
    IJwtService jwtService) : IAuthService
{
    private static readonly Regex HandleRegex = new(@"^[a-z0-9_]{3,20}$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^\S+@\S+\.\S+$", RegexOptions.Compiled);

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new DomainException("VALIDATION_ERROR", "Укажите фамилию", "lastName");

        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new DomainException("VALIDATION_ERROR", "Укажите имя", "firstName");

        if (!HandleRegex.IsMatch(request.Handle))
            throw new DomainException("VALIDATION_ERROR", "Handle must be 3-20 lowercase alphanumeric or underscore characters", "handle");

        if (!EmailRegex.IsMatch(request.Email))
            throw new DomainException("VALIDATION_ERROR", "Invalid email address", "email");

        if (request.Password.Length < 8)
            throw new DomainException("VALIDATION_ERROR", "Password must be at least 8 characters", "password");

        var normalizedEmail = request.Email.ToLowerInvariant();
        var normalizedHandle = request.Handle.ToLowerInvariant();

        if (await userRepo.FindByEmailAsync(normalizedEmail) is not null)
            throw new DomainException("EMAIL_TAKEN", "Email is already in use", "email");

        if (await userRepo.FindByHandleAsync(normalizedHandle) is not null)
            throw new DomainException("HANDLE_TAKEN", "Handle is already taken", "handle");

        var user = new User
        {
            Id = $"u_{Guid.NewGuid():N}",
            LastName = request.LastName.Trim(),
            FirstName = request.FirstName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim(),
            Handle = normalizedHandle,
            Email = normalizedEmail,
            PasswordHash = passwordHasher.Hash(request.Password)
        };

        await userRepo.AddAsync(user);
        await userRepo.SaveChangesAsync();

        var accessToken = jwtService.GenerateAccessToken(user);
        var refreshToken = await jwtService.GenerateRefreshTokenAsync(user.Id);

        return new AuthResponse(
            UserDto.From(user),
            new TokensDto(accessToken, refreshToken)
        );
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();
        var user = await userRepo.FindByEmailAsync(normalizedEmail)
                   ?? throw new DomainException("USER_NOT_FOUND", "Пользователь с такой почтой не найден", "email");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new DomainException("WRONG_PASSWORD", "Неверный пароль", "password");

        var accessToken = jwtService.GenerateAccessToken(user);
        var refreshToken = await jwtService.GenerateRefreshTokenAsync(user.Id);

        return new AuthResponse(
            UserDto.From(user),
            new TokensDto(accessToken, refreshToken)
        );
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken)
    {
        var (user, newRefreshToken) = await jwtService.RotateRefreshTokenAsync(refreshToken);

        var accessToken = jwtService.GenerateAccessToken(user);

        return new AuthResponse(
            UserDto.From(user),
            new TokensDto(accessToken, newRefreshToken)
        );
    }

    public async Task LogoutAsync(string refreshToken)
    {
        await jwtService.RevokeRefreshTokenAsync(refreshToken);
    }

    public async Task<UserDto> GetMeAsync(string userId)
    {
        var user = await userRepo.FindByIdAsync(userId)
                   ?? throw new DomainException("NOT_FOUND", "User not found");

        return UserDto.From(user);
    }

    public async Task<UserDto> UpdateProfileAsync(string userId, UpdateProfileRequest request)
    {
        var user = await userRepo.FindByIdAsync(userId)
                   ?? throw new DomainException("NOT_FOUND", "User not found");

        if (request.LastName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.LastName))
                throw new DomainException("VALIDATION_ERROR", "Укажите фамилию", "lastName");
            user.LastName = request.LastName.Trim();
        }

        if (request.FirstName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName))
                throw new DomainException("VALIDATION_ERROR", "Укажите имя", "firstName");
            user.FirstName = request.FirstName.Trim();
        }

        if (request.MiddleName is not null)
            user.MiddleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim();

        await userRepo.SaveChangesAsync();

        return UserDto.From(user);
    }
}
