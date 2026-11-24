using Auth.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var tokens = modelBuilder.Entity<RefreshToken>();
        tokens.ToTable("RefreshTokens");
        tokens.HasKey(token => token.Id);
        tokens.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        // A tracked revoke/rotate must fail if another request already consumed it.
        tokens.Property(token => token.RevokedAt).IsConcurrencyToken();
        tokens.HasIndex(token => token.TokenHash).IsUnique();
        tokens.HasIndex(token => new { token.UserId, token.ExpiresAt });
        tokens.HasOne(token => token.User).WithMany()
            .HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        tokens.HasOne<RefreshToken>().WithOne()
            .HasForeignKey<RefreshToken>(token => token.ReplacementTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
