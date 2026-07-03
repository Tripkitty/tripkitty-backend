using Microsoft.EntityFrameworkCore;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Infrastructure.Data;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string email) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public Task<User?> FindByHandleAsync(string handle) =>
        db.Users.FirstOrDefaultAsync(u => u.Handle == handle);

    public Task<User?> FindByIdAsync(string id) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public async Task AddAsync(User user) => await db.Users.AddAsync(user);

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
