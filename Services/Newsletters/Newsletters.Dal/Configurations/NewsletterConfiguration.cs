using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newsletters.Domain.Models;

namespace Newsletters.Dal.Configurations;

public sealed class NewsletterConfiguration : IEntityTypeConfiguration<Newsletter>
{
    public void Configure(EntityTypeBuilder<Newsletter> builder)
    {
        builder.ToTable("newsletters", "newsletter", table =>
        {
            table.HasCheckConstraint("ck_newsletters_publish_time", "is_hidden OR published_at IS NOT NULL");
            table.HasCheckConstraint("ck_newsletters_row_version", "row_version > 0");
        });
        builder.HasKey(newsletter => newsletter.Id);
        builder.Property(newsletter => newsletter.Id).HasColumnName("newsletter_id").ValueGeneratedOnAdd();
        builder.Property(newsletter => newsletter.AdminId).HasColumnName("admin_id").IsRequired();
        builder.Property(newsletter => newsletter.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(newsletter => newsletter.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValueSql("CURRENT_USER");
        builder.Property(newsletter => newsletter.PublishedAt).HasColumnName("published_at").HasColumnType("timestamp with time zone");
        builder.Property(newsletter => newsletter.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(newsletter => newsletter.MainInfo).HasColumnName("main_info").HasColumnType("text").IsRequired();
        builder.Property(newsletter => newsletter.ImageUrl).HasColumnName("image_url").HasMaxLength(2048);
        builder.Property(newsletter => newsletter.IsHidden).HasColumnName("is_hidden").HasDefaultValue(true);
        builder.Property(newsletter => newsletter.PublishedBy).HasColumnName("published_by").HasMaxLength(100);
        builder.Property(newsletter => newsletter.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(newsletter => newsletter.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(newsletter => newsletter.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(newsletter => newsletter.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasIndex(newsletter => newsletter.PublishedAt).IsDescending(true)
            .HasFilter("is_hidden = FALSE AND is_deleted = FALSE").HasDatabaseName("ix_newsletters_public_feed");
        builder.HasIndex(newsletter => new { newsletter.AdminId, newsletter.CreatedAt }).IsDescending(false, true)
            .HasDatabaseName("ix_newsletters_admin_history");
        builder.HasOne(newsletter => newsletter.DeliverySettings).WithOne(settings => settings.Newsletter)
            .HasForeignKey<NewsletterDeliverySettings>(settings => settings.NewsletterId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(newsletter => newsletter.Comments).WithOne(comment => comment.Newsletter)
            .HasForeignKey(comment => comment.NewsletterId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(newsletter => newsletter.Subscriptions).WithOne(subscription => subscription.Newsletter)
            .HasForeignKey(subscription => subscription.NewsletterId).OnDelete(DeleteBehavior.NoAction);
    }
}
