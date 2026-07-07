using Tripkitty.Application.DTOs;
using Tripkitty.Application.Logic;
using Tripkitty.Domain.Entities;
using Tripkitty.Domain.Exceptions;

namespace Tripkitty.Application.Services;

public interface IPaymentMethodService
{
    Task<List<PaymentMethodDto>> GetMineAsync(string userId);
    Task<PaymentMethodDto> CreateAsync(string userId, CreatePaymentMethodRequest request);
    Task<PaymentMethodDto> UpdateAsync(string userId, string id, UpdatePaymentMethodRequest request);
    Task DeleteAsync(string userId, string id);
}

public interface IPaymentMethodRepository
{
    Task<List<PaymentMethod>> GetForUserAsync(string userId);
    Task<PaymentMethod?> FindByIdAsync(string id);
    Task<List<PaymentMethod>> GetForUsersAsync(IEnumerable<string> userIds);
    Task AddAsync(PaymentMethod paymentMethod);
    void Remove(PaymentMethod paymentMethod);
    Task SaveChangesAsync();
}

public class PaymentMethodService(IPaymentMethodRepository repo) : IPaymentMethodService
{
    public async Task<List<PaymentMethodDto>> GetMineAsync(string userId)
    {
        var methods = await repo.GetForUserAsync(userId);
        return methods.Select(PaymentMethodDto.From).ToList();
    }

    public async Task<PaymentMethodDto> CreateAsync(string userId, CreatePaymentMethodRequest request)
    {
        var phone = PhoneNormalizer.Normalize(request.Phone);
        var banks = PaymentDetailsFactory.ValidateBanks(request.Banks);
        var label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim();

        var existing = await repo.GetForUserAsync(userId);

        // Первый способ становится дефолтным автоматически.
        var makeDefault = request.IsDefault || existing.Count == 0;
        if (makeDefault)
            foreach (var m in existing.Where(m => m.IsDefault))
                m.IsDefault = false;

        var method = new PaymentMethod
        {
            Id = $"pm_{Guid.NewGuid():N}",
            UserId = userId,
            Phone = phone,
            Banks = banks,
            Label = label,
            IsDefault = makeDefault
        };

        await repo.AddAsync(method);
        await repo.SaveChangesAsync();

        return PaymentMethodDto.From(method);
    }

    public async Task<PaymentMethodDto> UpdateAsync(string userId, string id, UpdatePaymentMethodRequest request)
    {
        var method = await repo.FindByIdAsync(id);
        if (method is null || method.UserId != userId)
            throw new DomainException("PAYMENT_METHOD_NOT_FOUND", "Способ оплаты не найден");

        if (request.Phone is not null)
            method.Phone = PhoneNormalizer.Normalize(request.Phone);

        if (request.Banks is not null)
            method.Banks = PaymentDetailsFactory.ValidateBanks(request.Banks);

        if (request.Label is not null)
            method.Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim();

        if (request.IsDefault == true && !method.IsDefault)
        {
            var siblings = await repo.GetForUserAsync(userId);
            foreach (var m in siblings.Where(m => m.IsDefault))
                m.IsDefault = false;
            method.IsDefault = true;
        }

        await repo.SaveChangesAsync();

        return PaymentMethodDto.From(method);
    }

    public async Task DeleteAsync(string userId, string id)
    {
        var method = await repo.FindByIdAsync(id);
        if (method is null || method.UserId != userId)
            throw new DomainException("PAYMENT_METHOD_NOT_FOUND", "Способ оплаты не найден");

        var wasDefault = method.IsDefault;
        repo.Remove(method);

        // Если удалили дефолтный — назначаем дефолтным любой оставшийся.
        if (wasDefault)
        {
            var remaining = (await repo.GetForUserAsync(userId))
                .Where(m => m.Id != id)
                .ToList();
            var next = remaining.FirstOrDefault();
            if (next is not null)
                next.IsDefault = true;
        }

        await repo.SaveChangesAsync();
    }
}
