using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Tripkitty.Application.Services;
using Tripkitty.Infrastructure.Data;
using Tripkitty.Infrastructure.Services;

namespace Tripkitty.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        var dataSource = new NpgsqlDataSourceBuilder(configuration.GetConnectionString("Default"))
            .EnableDynamicJson()
            .Build();
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(dataSource));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IFriendshipRepository, FriendshipRepository>();
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();

        // Infrastructure services
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IIcsService, IcsService>();
        services.AddScoped<IPushNotificationService, WebPushService>();

        // Application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITripService, TripService>();
        services.AddScoped<IParticipantService, ParticipantService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();

        // WebPush VAPID
        services.Configure<WebPushOptions>(configuration.GetSection("WebPush"));

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
                // SignalR WebSocket passes token via query string
                options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        // Health checks
        services.AddHealthChecks()
            .AddCheck<DbHealthCheck>("database");

        // CORS
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
