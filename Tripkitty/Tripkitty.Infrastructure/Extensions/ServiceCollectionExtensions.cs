using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Tripkitty.Application.Services;
using Tripkitty.Infrastructure.Data;
using Tripkitty.Infrastructure.Services;

namespace Tripkitty.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IFriendshipRepository, FriendshipRepository>();

        // Infrastructure services
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IIcsService, IcsService>();

        // Application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITripService, TripService>();
        services.AddScoped<IParticipantService, ParticipantService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<IEventService, EventService>();

        // JWT Authentication
        var jwtKey = configuration["Jwt:Key"]
                     ?? throw new InvalidOperationException("Jwt:Key not configured");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "tripkitty-api";
        var jwtAudience = configuration["Jwt:Audience"] ?? "tripkitty-client";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });

        services.AddAuthorization();

        return services;
    }
}
