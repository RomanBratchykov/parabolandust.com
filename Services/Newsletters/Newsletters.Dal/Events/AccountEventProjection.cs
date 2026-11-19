using Microsoft.EntityFrameworkCore;
using Newsletters.Dal.Data;
using Platform.Messaging.Events;

namespace Newsletters.Dal.Events;

public sealed class AccountEventProjection(NewslettersDbContext context) : IAccountEventProjection
{
    public async Task ApplyAsync(Guid eventId, AccountSnapshot account, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var inserted = await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO newsletter.processed_events (event_id) VALUES ({eventId})
            ON CONFLICT (event_id) DO NOTHING
            """, cancellationToken);
        if (inserted != 0)
            await ApplySnapshotAsync(account, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<int> ApplySnapshotAsync(AccountSnapshot account, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO newsletter.users_copy
                (user_id, user_name, user_email, created_at, source_updated_at, source_version, is_deleted)
            VALUES ({account.UserId}, {account.UserName}, {account.UserEmail}, {account.CreatedAt.ToUniversalTime()},
                {account.SourceUpdatedAt.ToUniversalTime()}, {account.SourceVersion}, {account.IsDeleted})
            ON CONFLICT (user_id) DO UPDATE SET
                user_name = EXCLUDED.user_name,
                user_email = EXCLUDED.user_email,
                source_updated_at = EXCLUDED.source_updated_at,
                source_version = EXCLUDED.source_version,
                is_deleted = EXCLUDED.is_deleted,
                updated_at = CURRENT_TIMESTAMP,
                updated_by = 'account-events',
                row_version = users_copy.row_version + 1
            WHERE users_copy.source_version < EXCLUDED.source_version
            """, cancellationToken);
}
