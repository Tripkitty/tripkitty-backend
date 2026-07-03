using Microsoft.EntityFrameworkCore;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;
using Tripkitty.Infrastructure.Data;

public static class DevSeeder
{
    private static readonly (string Handle, string Name, string Email, string Password)[] TestUsers =
    [
        ("test1", "Test User 1", "test1@test.local", "testtest1"),
        ("test2", "Test User 2", "test2@test.local", "testtest2"),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher>();

        var existingHandles = await db.Users
            .Where(u => TestUsers.Select(t => t.Handle).Contains(u.Handle))
            .Select(u => u.Handle)
            .ToListAsync();

        var toSeed = TestUsers.Where(t => !existingHandles.Contains(t.Handle)).ToList();
        if (toSeed.Count == 0) return;

        foreach (var (handle, name, email, password) in toSeed)
        {
            db.Users.Add(new User
            {
                Handle = handle,
                Name = name,
                Email = email,
                PasswordHash = hasher.Hash(password),
            });
        }

        await db.SaveChangesAsync();
    }
}
