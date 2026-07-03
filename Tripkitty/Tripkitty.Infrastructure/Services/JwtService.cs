using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;
using Tripkitty.Infrastructure.Data;

namespace Tripkitty.Infrastructure.Services;

public class JwtService(AppDbContext db, IConfiguration configuration) : IJwtService
{
    private string Key => configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
    private string Issuer => configuration["Jwt:Issuer"] ?? "tripkitty-api";
    private string Audience => configuration["Jwt:Audience"] ?? "tripkitty-client";
    private int AccessTokenExpiryMinutes => int.Parse(configuration["Jwt:AccessTokenExpiryMinutes"] ?? "15");
    private int RefreshTokenExpiryDays => int.Parse(configuration["Jwt:RefreshTokenExpiryDays"] ?? "30");

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim("handle", user.Handle),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> GenerateRefreshTokenAsync(string userId)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = HashToken(rawToken);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays),
            IsRevoked = false
        };

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        return rawToken;
    }

    public async Task<User?> ValidateRefreshTokenAsync(string rawToken)
    {
        var tokenHash = HashToken(rawToken);

        var token = await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash && !r.IsRevoked && r.ExpiresAt > DateTime.UtcNow);

        return token?.User;
    }

    public async Task RevokeRefreshTokenAsync(string rawToken)
    {
        var tokenHash = HashToken(rawToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == tokenHash);
        if (token is not null)
        {
            token.IsRevoked = true;
            await db.SaveChangesAsync();
        }
    }

    public async Task<(User user, string newRawToken)> RotateRefreshTokenAsync(string rawToken)
    {
        var tokenHash = HashToken(rawToken);

        var existing = await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash && !r.IsRevoked && r.ExpiresAt > DateTime.UtcNow)
            ?? throw new Tripkitty.Domain.Exceptions.DomainException("INVALID_TOKEN", "Refresh token is invalid or expired");

        existing.IsRevoked = true;

        var newRawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var newHash = HashToken(newRawToken);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            TokenHash = newHash,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays),
            IsRevoked = false
        });

        await db.SaveChangesAsync();

        return (existing.User, newRawToken);
    }

    private static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(bytes);
    }
}
