using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace Accounts.Dal.Repositories;

public sealed class IdentityLinkRepository(string connectionString)
{
    public async Task<long?> ResolveAsync(string issuer, string subject, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return await ResolveAsync(connection, null, issuer, subject, cancellationToken);
    }

    public async Task<long?> ProvisionAsync(string issuer, string subject, string userName, string email,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        // Serialize concurrent provisioning for this exact identity without trusting a client-supplied account ID.
        await using (var gate = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@issuer || chr(31) || @subject, 0))", connection, transaction))
        {
            AddIdentity(gate, issuer, subject);
            await gate.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var lookup = new NpgsqlCommand("""
            SELECT e.user_id, e.revoked_at IS NOT NULL OR u.is_deleted
            FROM accounts.external_identities e JOIN accounts.users u ON u.user_id = e.user_id
            WHERE e.issuer = @issuer AND e.subject = @subject
            """, connection, transaction))
        {
            AddIdentity(lookup, issuer, subject);
            await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var existing = reader.GetBoolean(1) ? (long?)null : reader.GetInt64(0);
                await reader.DisposeAsync();
                await transaction.CommitAsync(cancellationToken);
                return existing;
            }
        }

        long userId;
        await using (var create = new NpgsqlCommand("""
            INSERT INTO accounts.users (user_name, user_email, identity_managed, user_whoami)
            VALUES (@name, @email, TRUE, @whoami) RETURNING user_id
            """, connection, transaction))
        {
            create.Parameters.AddWithValue("name", NpgsqlDbType.Text, userName);
            create.Parameters.AddWithValue("email", NpgsqlDbType.Text, email);
            create.Parameters.AddWithValue("whoami", NpgsqlDbType.Text, "player-" + Guid.NewGuid().ToString("N"));
            userId = (long)(await create.ExecuteScalarAsync(cancellationToken))!;
        }
        await using (var link = new NpgsqlCommand("""
            INSERT INTO accounts.external_identities (issuer, subject, user_id) VALUES (@issuer, @subject, @userId)
            """, connection, transaction))
        {
            AddIdentity(link, issuer, subject);
            link.Parameters.AddWithValue("userId", NpgsqlDbType.Bigint, userId);
            await link.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return userId;
    }

    private static async Task<long?> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        string issuer, string subject, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT e.user_id FROM accounts.external_identities e
            JOIN accounts.users u ON u.user_id = e.user_id
            WHERE e.issuer = @issuer AND e.subject = @subject AND e.revoked_at IS NULL AND u.is_deleted IS FALSE
            """, connection, transaction);
        AddIdentity(command, issuer, subject);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is long userId ? userId : null;
    }

    private static void AddIdentity(NpgsqlCommand command, string issuer, string subject)
    {
        command.Parameters.AddWithValue("issuer", NpgsqlDbType.Text, issuer);
        command.Parameters.AddWithValue("subject", NpgsqlDbType.Text, subject);
    }
}
