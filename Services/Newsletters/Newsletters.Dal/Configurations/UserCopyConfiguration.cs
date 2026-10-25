using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newsletters.Domain.Models;

namespace Newsletters.Dal.Configurations;

public sealed class UserCopyConfiguration : IEntityTypeConfiguration<UserCopy>
{
    public void Configure(EntityTypeBuilder<UserCopy> builder)
    {
        builder.ToTable("users_copy", "newsletter", table =>
            table.HasCheckConstraint("ck_users_copy_row_version", "row_version > 0"));
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(user => user.UserName).HasColumnName("user_name").HasMaxLength(50).IsRequired();
        builder.Property(user => user.Email).HasColumnName("user_email").HasMaxLength(254);
        builder.Property(user => user.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(user => user.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValue("account-events");
        builder.Property(user => user.SourceUpdatedAt).HasColumnName("source_updated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(user => user.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(user => user.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100).HasDefaultValue("account-events");
        builder.Property(user => user.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(user => user.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasMany(user => user.Comments).WithOne(comment => comment.User)
            .HasForeignKey(comment => comment.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(user => user.Subscriptions).WithOne(subscription => subscription.User)
            .HasForeignKey(subscription => subscription.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasData(new
        {
            Id = 1001L,
            UserName = "alex_player",
            Email = "alex@example.test",
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = "account-events",
            SourceUpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedBy = "account-events",
            IsDeleted = false,
            RowVersion = 1L
        });
    }
}
