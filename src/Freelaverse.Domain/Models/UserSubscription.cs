using System.ComponentModel.DataAnnotations;

namespace FreelaverseApi.Models;

public class UserSubscription : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    [Required, MaxLength(120)]
    public string StripeCustomerId { get; set; } = string.Empty;

    // Dados adicionais capturados pelo webhook do Stripe
    [MaxLength(120)]
    public string? StripeSubscriptionId { get; set; }

    [MaxLength(120)]
    public string? StripePriceId { get; set; }

    public DateTimeOffset? StripeCurrentPeriodEnd { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}
