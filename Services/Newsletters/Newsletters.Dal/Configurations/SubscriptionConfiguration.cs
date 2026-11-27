using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newsletters.Domain.Models;

namespace Newsletters.Dal.Configurations;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions", "newsletter", table =>
        {
            table.HasCheckConstraint("ck_subscriptions_status", "sub_status IN ('Active', 'Unsubscribed')");
            table.HasCheckConstraint("ck_subscriptions_row_version", "row_version > 0");
        });
        builder.HasKey(subscription => subscription.Id);
        builder.Property(subscription => subscription.Id).HasColumnName("subscription_id").ValueGeneratedOnAdd();
        builder.Property(subscription => subscription.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(subscription => subscription.NewsletterId).HasColumnName("newsletter_id").IsRequired();
        builder.Property(subscription => subscription.Status).HasColumnName("sub_status").HasConversion<string>()
            .HasMaxLength(20).HasDefaultValue(SubscriptionStatus.Active).IsRequired();
        builder.Property(subscription => subscription.ConsentAt).HasColumnName("consent_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(subscription => subscription.UnsubscribeToken).HasColumnName("unsubscribe_token").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(subscription => subscription.UserEmailSnapshot).HasColumnName("user_email_snapshot").HasMaxLength(254);
        builder.Property(subscription => subscription.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(subscription => subscription.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValueSql("CURRENT_USER");
        builder.Property(subscription => subscription.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(subscription => subscription.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(subscription => subscription.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(subscription => subscription.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasIndex(subscription => subscription.UnsubscribeToken).IsUnique().HasDatabaseName("subscriptions_unsubscribe_token_key");
        builder.HasIndex(subscription => new { subscription.UserId, subscription.NewsletterId }).IsUnique()
            .HasDatabaseName("uq_subscriptions_user_newsletter");
        builder.HasIndex(subscription => new { subscription.NewsletterId, subscription.UserId })
            .HasFilter("sub_status = 'Active' AND is_deleted = FALSE").HasDatabaseName("ix_subscriptions_delivery");
    }
}
