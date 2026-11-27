using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newsletters.Domain.Models;

namespace Newsletters.Dal.Configurations;

public sealed class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> builder)
    {
        builder.ToTable("admins", "newsletter", table =>
            table.HasCheckConstraint("ck_admins_row_version", "row_version > 0"));
        builder.HasKey(admin => admin.Id);
        builder.Property(admin => admin.Id).HasColumnName("admin_id").ValueGeneratedOnAdd();
        builder.Property(admin => admin.Login).HasColumnName("admin_login").HasMaxLength(80).IsRequired();
        builder.Property(admin => admin.PasswordHash).HasColumnName("admin_password_hash").HasMaxLength(255).IsRequired();
        builder.Property(admin => admin.Name).HasColumnName("admin_name").HasMaxLength(120).IsRequired();
        builder.Property(admin => admin.Email).HasColumnName("admin_email").HasMaxLength(254).IsRequired();
        builder.Property(admin => admin.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(admin => admin.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValueSql("CURRENT_USER");
        builder.Property(admin => admin.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(admin => admin.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(admin => admin.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(admin => admin.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasIndex(admin => admin.Login).IsUnique().HasDatabaseName("admins_admin_login_key");
        builder.HasMany(admin => admin.Newsletters).WithOne(newsletter => newsletter.Admin)
            .HasForeignKey(newsletter => newsletter.AdminId).OnDelete(DeleteBehavior.NoAction);
        builder.HasData(new
        {
            Id = 4001L,
            Login = "editor_demo",
            PasswordHash = "demo-password-hash",
            Name = "Demo Editor",
            Email = "adm1@gmail.com",
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = "seed",
            IsDeleted = false,
            RowVersion = 1L
        });
    }
}
