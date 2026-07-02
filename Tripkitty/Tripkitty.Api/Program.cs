using Microsoft.EntityFrameworkCore;
using Serilog;
using Tripkitty.Api.Endpoints;
using Tripkitty.Api.Hubs;
using Tripkitty.Api.Middleware;
using Tripkitty.Application.Services;
using Tripkitty.Infrastructure.Data;
using Tripkitty.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<ITripNotifier, SignalRTripNotifier>();
builder.Services.AddScoped<IFriendNotifier, SignalRFriendNotifier>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (app.Environment.IsDevelopment())
        await DevSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionMiddleware>();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapTripEndpoints();
app.MapFriendEndpoints();
app.MapNotificationEndpoints();
app.MapHub<TripHub>("/hubs/trip");
app.MapHealthChecks("/health");

app.Run();
