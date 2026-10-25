using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newsletters.Domain.Models;

namespace Newsletters.Dal.Configurations;

public sealed class UserCommentConfiguration : IEntityTypeConfiguration<UserComment>
{
    public void Configure(EntityTypeBuilder<UserComment> builder)
    {
        builder.ToTable("user_comments", "newsletter", table =>
            table.HasCheckConstraint("ck_user_comments_row_version", "row_version > 0"));
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Id).HasColumnName("comment_id").ValueGeneratedOnAdd();
        builder.Property(comment => comment.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(comment => comment.NewsletterId).HasColumnName("newsletter_id").IsRequired();
        builder.Property(comment => comment.ParentCommentId).HasColumnName("parent_comment_id");
        builder.Property(comment => comment.CommentText).HasColumnName("comment_text").HasMaxLength(4000).IsRequired();
        builder.Property(comment => comment.IsHidden).HasColumnName("is_hidden").HasDefaultValue(false);
        builder.Property(comment => comment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(comment => comment.CreatedBy).HasColumnName("created_by").HasMaxLength(100).HasDefaultValueSql("CURRENT_USER");
        builder.Property(comment => comment.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(comment => comment.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(comment => comment.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(comment => comment.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasIndex(comment => new { comment.NewsletterId, comment.CreatedAt }).IsDescending(false, false)
            .HasFilter("is_deleted = FALSE").HasDatabaseName("ix_comments_newsletter_thread");
        builder.HasOne(comment => comment.ParentComment).WithMany(comment => comment.Replies)
            .HasForeignKey(comment => comment.ParentCommentId).OnDelete(DeleteBehavior.NoAction);
    }
}
