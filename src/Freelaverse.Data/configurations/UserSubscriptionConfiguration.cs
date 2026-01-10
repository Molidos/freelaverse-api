using FreelaverseApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("UserSubscriptions");

        builder.HasIndex(x => x.UserId).IsUnique();

        builder.Property(x => x.StripeCustomerId)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.StripeSubscriptionId)
            .HasMaxLength(120);

        builder.Property(x => x.StripePriceId)
            .HasMaxLength(120);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
