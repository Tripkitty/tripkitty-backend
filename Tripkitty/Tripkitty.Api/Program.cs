using Serilog;
using Tripkitty.Api.Endpoints;
using Tripkitty.Api.Hubs;
using Tripkitty.Api.Middleware;
using Tripkitty.Application.Services;
using Tripkitty.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<ITripNotifier, SignalRTripNotifier>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapTripEndpoints();
app.MapFriendEndpoints();
app.MapNotificationEndpoints();
app.MapHub<TripHub>("/hubs/trip");
app.MapHealthChecks("/health");

app.Run();
