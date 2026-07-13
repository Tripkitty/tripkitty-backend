using Tripkitty.Application.DTOs;

namespace Tripkitty.Application.Services;

public interface IAdminStatsService
{
    Task<AdminStatsDto> GetStatsAsync();
}

public interface IAdminStatsRepository
{
    Task<int> CountUsersAsync();
    Task<int> CountUsersCreatedSinceAsync(DateTime since);
    Task<int> CountTripsByStatusAsync(int status);
    Task<int> CountTripsAsync();
    Task<int> CountExpensesAsync();
    Task<int> CountGuestsAsync();
    Task<int> CountFriendshipsAsync();
}

public class AdminStatsService(IAdminStatsRepository repo) : IAdminStatsService
{
    public async Task<AdminStatsDto> GetStatsAsync()
    {
        var now = DateTime.UtcNow;

        return new AdminStatsDto(
            TotalUsers: await repo.CountUsersAsync(),
            NewUsersToday: await repo.CountUsersCreatedSinceAsync(now.Date),
            NewUsersThisWeek: await repo.CountUsersCreatedSinceAsync(now.AddDays(-7)),
            NewUsersThisMonth: await repo.CountUsersCreatedSinceAsync(now.AddDays(-30)),
            TotalTrips: await repo.CountTripsAsync(),
            ActiveTrips: await repo.CountTripsByStatusAsync(0),
            SettlingTrips: await repo.CountTripsByStatusAsync(1),
            SettledTrips: await repo.CountTripsByStatusAsync(2),
            TotalExpenses: await repo.CountExpensesAsync(),
            TotalGuests: await repo.CountGuestsAsync(),
            TotalFriendships: await repo.CountFriendshipsAsync());
    }
}
