using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newsletters.Domain.Models;

namespace Newsletters.Dal.Configurations;

public sealed class NewsletterDeliverySettingsConfiguration : IEntityTypeConfiguration<NewsletterDeliverySettings>
{
    public void Configure(EntityTypeBuilder<NewsletterDeliverySettings> builder)
    {
        builder.ToTable("newsletter_delivery_settings", "newsletter", table =>
            table.HasCheckConstraint("ck_newsletter_delivery_settings_row_version", "row_version > 0"));
        builder.HasKey(settings => settings.NewsletterId);
        builder.Property(settings => settings.NewsletterId).HasColumnName("newsletter_id").ValueGeneratedNever();
        builder.Property(settings => settings.ScheduledAt).HasColumnName("scheduled_at").HasColumnType("timestamp with time zone");
        builder.Property(settings => settings.RecipientSettings).HasColumnName("recipient_settings")
            .HasColumnType("jsonb").HasDefaultValueSql("'{\"subscribers\": true}'::jsonb").IsRequired();
        builder.Property(settings => settings.ReplyToEmail).HasColumnName("reply_to_email").HasMaxLength(254);
        builder.Property(settings => settings.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(settings => settings.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValueSql("CURRENT_USER");
        builder.Property(settings => settings.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(settings => settings.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(settings => settings.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(settings => settings.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken();
    }
}
