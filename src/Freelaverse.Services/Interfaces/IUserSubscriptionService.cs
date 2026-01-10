using FreelaverseApi.Models;

namespace Freelaverse.Services.Interfaces;

public interface IUserSubscriptionService
{
    Task<UserSubscription?> GetByUserIdAsync(Guid userId);
    Task<UserSubscription?> GetByCustomerIdAsync(string stripeCustomerId);
    Task<UserSubscription> UpsertAsync(Guid userId, string stripeCustomerId);
    Task<UserSubscription> UpsertFromWebhookAsync(
        Guid userId,
        string stripeCustomerId,
        string stripeSubscriptionId,
        string stripePriceId,
        DateTimeOffset currentPeriodEnd);
}
