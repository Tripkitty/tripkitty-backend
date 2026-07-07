using Microsoft.EntityFrameworkCore;
using Tripkitty.Application.Services;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Infrastructure.Data;

public class PaymentMethodRepository(AppDbContext db) : IPaymentMethodRepository
{
    public Task<List<PaymentMethod>> GetForUserAsync(string userId) =>
        db.PaymentMethods
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.IsDefault)
            .ToListAsync();

    public Task<PaymentMethod?> FindByIdAsync(string id) =>
        db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id);

    public Task<List<PaymentMethod>> GetForUsersAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.ToList();
        if (ids.Count == 0)
            return Task.FromResult(new List<PaymentMethod>());

        return db.PaymentMethods
            .Where(p => ids.Contains(p.UserId))
            .ToListAsync();
    }

    public async Task AddAsync(PaymentMethod paymentMethod) => await db.PaymentMethods.AddAsync(paymentMethod);

    public void Remove(PaymentMethod paymentMethod) => db.PaymentMethods.Remove(paymentMethod);

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
