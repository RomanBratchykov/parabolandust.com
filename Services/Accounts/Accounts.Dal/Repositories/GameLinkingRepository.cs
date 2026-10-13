using Accounts.Domain.Models;
using Dapper;
using Npgsql;

namespace Accounts.Dal.Repositories;

public sealed class GameLinkingRepository : BaseDapperRepository<GameLinking>, IGameLinkingRepository
{
    public GameLinkingRepository(NpgsqlConnection connection, Func<NpgsqlTransaction?> currentTransaction)
        : base(
            connection,
            currentTransaction,
            "accounts.game_linking",
            "user_id",
            """
            INSERT INTO accounts.game_linking (user_id, connecting_hash)
            VALUES (@UserId, @ConnectingHash)
            RETURNING user_id
            """,
            linking => new { linking.UserId, linking.ConnectingHash },
            """
            UPDATE accounts.game_linking
            SET connecting_hash = @ConnectingHash,
                updated_at = CURRENT_TIMESTAMP,
                updated_by = CURRENT_USER,
                row_version = row_version + 1
            WHERE user_id = @UserId AND row_version = @RowVersion
            """,
            linking => new { linking.UserId, linking.ConnectingHash, linking.RowVersion },
            """
            UPDATE accounts.game_linking
            SET removed_at = CURRENT_TIMESTAMP,
                updated_at = CURRENT_TIMESTAMP,
                updated_by = CURRENT_USER,
                row_version = row_version + 1
            WHERE user_id = @Id AND removed_at IS NULL
            """)
    {
    }

    public async Task<GameLinking?> GetByUserIdAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        const string sql = "SELECT * FROM accounts.game_linking WHERE user_id = @UserId";
        var command = new CommandDefinition(
            sql, new { UserId = userId }, Transaction, cancellationToken: cancellationToken);
        return await Connection.QuerySingleOrDefaultAsync<GameLinking>(command);
    }
}
