namespace Tripkitty.Application.DTOs;

public record AdminStatsDto(
    int TotalUsers,
    int NewUsersToday,
    int NewUsersThisWeek,
    int NewUsersThisMonth,
    int TotalTrips,
    int ActiveTrips,
    int SettlingTrips,
    int SettledTrips,
    int TotalExpenses,
    int TotalGuests,
    int TotalFriendships);
