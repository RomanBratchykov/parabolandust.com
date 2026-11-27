using System.Security.Cryptography;
using System.Text;
using Auth.Api.Data;
using Auth.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Tokens;

public sealed class RefreshTokenService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IJwtTokenService tokenService) : IRefreshTokenService
{
    public async Task<RefreshTokenGrant?> IssueAsync(string userId, string securityStamp, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockEligibleUserAsync(db, userId, securityStamp, cancellationToken))
            return null;
        var token = tokenService.GenerateRefreshToken();
        var session = CreateSession(userId, securityStamp, token, DateTimeOffset.UtcNow);
        db.RefreshTokens.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RefreshTokenGrant(userId, token, session.ExpiresAt);
    }

    public async Task<RefreshTokenGrant?> RotateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 256)
            return null;

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var hash = HashToken(token);
        var now = DateTimeOffset.UtcNow;
        var previous = await db.RefreshTokens.SingleOrDefaultAsync(
            session => session.TokenHash == hash && session.RevokedAt == null && session.ExpiresAt > now,
            cancellationToken);
        if (previous is null)
            return null;

        if (!await LockEligibleUserAsync(db, previous.UserId, previous.SecurityStamp, cancellationToken))
            return null;

        now = DateTimeOffset.UtcNow;
        if (previous.ExpiresAt <= now)
            return null;

        var replacementToken = tokenService.GenerateRefreshToken();
        var replacement = CreateSession(previous.UserId, previous.SecurityStamp, replacementToken, now);
        previous.RevokedAt = now;
        previous.ReplacementTokenId = replacement.Id;
        db.RefreshTokens.Add(replacement);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RefreshTokenGrant(previous.UserId, replacementToken, replacement.ExpiresAt);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Disposal rolls back the losing replacement as well as its revocation.
            return null;
        }
    }

    public async Task<bool> RevokeAsync(string userId, string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (string.IsNullOrWhiteSpace(token) || token.Length > 256)
            return false;

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var hash = HashToken(token);
        var now = DateTimeOffset.UtcNow;
        var affected = await db.RefreshTokens
            .Where(session => session.UserId == userId && session.TokenHash == hash && session.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, (DateTimeOffset?)now),
                cancellationToken);
        return affected == 1;
    }

    // Updating the same stamp takes a row lock shared with password changes.
    private static async Task<bool> LockEligibleUserAsync(ApplicationDbContext db, string userId,
        string securityStamp, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var affected = await db.Users
            .Where(user => user.Id == userId && user.SecurityStamp == securityStamp &&
                user.EmailConfirmed && !user.TwoFactorEnabled &&
                (user.LockoutEnd == null || user.LockoutEnd <= now))
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.SecurityStamp, user => user.SecurityStamp),
                cancellationToken);
        return affected == 1;
    }

    private static RefreshToken CreateSession(string userId, string securityStamp, string token, DateTimeOffset now) => new()
    {
        UserId = userId,
        SecurityStamp = securityStamp,
        TokenHash = HashToken(token),
        CreatedAt = now,
        ExpiresAt = now.AddDays(7)
    };

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
