using Accounts.Domain.Models;
using Npgsql;
using NpgsqlTypes;

namespace Accounts.Dal.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly NpgsqlConnection _connection;
    private readonly Func<NpgsqlTransaction?> _currentTransaction;

    public UserRepository(
        NpgsqlConnection connection,
        Func<NpgsqlTransaction?> currentTransaction)
    {
        _connection = connection;
        _currentTransaction = currentTransaction;
    }

    public async Task<AccountUser?> GetByIdAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT user_id, user_name, user_email, password_hash, oauth_provider, oauth_id,
                   created_at, created_by, updated_at, updated_by, user_whoami,
                   user_photo_url, row_version, is_deleted
            FROM accounts.users
            WHERE user_id = @userId AND is_deleted IS FALSE
            """;

        await EnsureOpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, _connection, _currentTransaction());
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Bigint, userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AccountUser
        {
            UserId = reader.GetInt64(0),
            UserName = reader.GetString(1),
            UserEmail = reader.GetString(2),
            PasswordHash = ReadNullableString(reader, 3),
            OAuthProvider = ReadNullableString(reader, 4),
            OAuthId = ReadNullableString(reader, 5),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(6),
            CreatedBy = ReadNullableString(reader, 7),
            UpdatedAt = ReadNullableDateTimeOffset(reader, 8),
            UpdatedBy = ReadNullableString(reader, 9),
            UserWhoAmI = reader.GetString(10),
            UserPhotoUrl = ReadNullableString(reader, 11),
            RowVersion = reader.GetInt64(12),
            IsDeleted = reader.GetBoolean(13)
        };
    }

    public async Task<long> AddAsync(
        AccountUser user,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO accounts.users
                (user_name, user_email, password_hash, oauth_provider, oauth_id,
                 user_whoami, user_photo_url)
            VALUES
                (@userName, @userEmail, @passwordHash, @oauthProvider, @oauthId,
                 @userWhoAmI, @userPhotoUrl)
            RETURNING user_id
            """;

        await EnsureOpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, _connection, _currentTransaction());
        AddTextParameter(command, "userName", user.UserName);
        AddTextParameter(command, "userEmail", user.UserEmail);
        AddTextParameter(command, "passwordHash", user.PasswordHash);
        AddTextParameter(command, "oauthProvider", user.OAuthProvider);
        AddTextParameter(command, "oauthId", user.OAuthId);
        AddTextParameter(command, "userWhoAmI", user.UserWhoAmI);
        AddTextParameter(command, "userPhotoUrl", user.UserPhotoUrl);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<bool> UpdateProfileAsync(
        AccountUser user,
        long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            CALL accounts.usp_user_update_profile(
                @userId, @userName, @userEmail, @userPhotoUrl, @expectedRowVersion)
            """;

        await EnsureOpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, _connection, _currentTransaction());
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Bigint, user.UserId);
        AddTextParameter(command, "userName", user.UserName);
        AddTextParameter(command, "userEmail", user.UserEmail);
        AddTextParameter(command, "userPhotoUrl", user.UserPhotoUrl);
        command.Parameters.AddWithValue("expectedRowVersion", NpgsqlDbType.Bigint, expectedRowVersion);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SoftDeleteAsync(
        long userId,
        long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        const string sql = "CALL accounts.usp_user_soft_delete(@userId, @expectedRowVersion)";

        await EnsureOpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, _connection, _currentTransaction());
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Bigint, userId);
        command.Parameters.AddWithValue("expectedRowVersion", NpgsqlDbType.Bigint, expectedRowVersion);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private async Task EnsureOpenAsync(CancellationToken cancellationToken)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }
    }

    private static void AddTextParameter(NpgsqlCommand command, string name, string? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Varchar);
        parameter.Value = (object?)value ?? DBNull.Value;
    }

    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static DateTimeOffset? ReadNullableDateTimeOffset(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
}
