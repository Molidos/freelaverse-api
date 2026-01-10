using FreelaverseApi.Data;
using FreelaverseApi.Models;
using Freelaverse.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Freelaverse.Data.Services;

public class UserSubscriptionService : IUserSubscriptionService
{
    private readonly AppDbContext _context;

    public UserSubscriptionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserSubscription?> GetByUserIdAsync(Guid userId)
    {
        return await _context.UserSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task<UserSubscription?> GetByCustomerIdAsync(string stripeCustomerId)
    {
        return await _context.UserSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StripeCustomerId == stripeCustomerId);
    }

    public async Task<UserSubscription> UpsertAsync(Guid userId, string stripeCustomerId)
    {
        var existing = await _context.UserSubscriptions
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (existing is null)
        {
            var entity = new UserSubscription
            {
                UserId = userId,
                StripeCustomerId = stripeCustomerId
            };

            _context.UserSubscriptions.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        existing.StripeCustomerId = stripeCustomerId;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<UserSubscription> UpsertFromWebhookAsync(
        Guid userId,
        string stripeCustomerId,
        string stripeSubscriptionId,
        string stripePriceId,
        DateTimeOffset currentPeriodEnd)
    {
        var existing = await _context.UserSubscriptions
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (existing is null)
        {
            existing = new UserSubscription
            {
                UserId = userId,
                StripeCustomerId = stripeCustomerId
            };
            _context.UserSubscriptions.Add(existing);
        }

        existing.StripeCustomerId = stripeCustomerId;
        existing.StripeSubscriptionId = stripeSubscriptionId;
        existing.StripePriceId = stripePriceId;
        existing.StripeCurrentPeriodEnd = currentPeriodEnd;

        await _context.SaveChangesAsync();
        return existing;
    }
}
