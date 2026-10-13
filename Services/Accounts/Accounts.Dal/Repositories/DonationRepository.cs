using Accounts.Domain.Models;
using Dapper;
using Npgsql;

namespace Accounts.Dal.Repositories;

public sealed class DonationRepository : BaseDapperRepository<Donation>, IDonationRepository
{
    public DonationRepository(NpgsqlConnection connection, Func<NpgsqlTransaction?> currentTransaction)
        : base(
            connection,
            currentTransaction,
            "accounts.donations",
            "donation_id",
            """
            INSERT INTO accounts.donations
                (user_id, donation_amount, commentary, currency, is_anonymous, provider_payment_id)
            VALUES
                (@UserId, @DonationAmount, @Commentary, @Currency, @IsAnonymous, @ProviderPaymentId)
            RETURNING donation_id
            """,
            donation => new
            {
                donation.UserId,
                donation.DonationAmount,
                donation.Commentary,
                donation.Currency,
                donation.IsAnonymous,
                donation.ProviderPaymentId
            },
            """
            UPDATE accounts.donations
            SET donation_amount = @DonationAmount,
                commentary = @Commentary,
                currency = @Currency,
                is_anonymous = @IsAnonymous,
                provider_payment_id = @ProviderPaymentId,
                updated_at = CURRENT_TIMESTAMP,
                updated_by = CURRENT_USER,
                row_version = row_version + 1
            WHERE donation_id = @DonationId
              AND row_version = @RowVersion
              AND is_deleted IS FALSE
            """,
            donation => new
            {
                donation.DonationId,
                donation.DonationAmount,
                donation.Commentary,
                donation.Currency,
                donation.IsAnonymous,
                donation.ProviderPaymentId,
                donation.RowVersion
            },
            """
            UPDATE accounts.donations
            SET is_deleted = TRUE,
                updated_at = CURRENT_TIMESTAMP,
                updated_by = CURRENT_USER,
                row_version = row_version + 1
            WHERE donation_id = @Id AND is_deleted IS FALSE
            """,
            "is_deleted IS FALSE")
    {
    }

    public async Task<IReadOnlyList<Donation>> GetForUserAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        const string sql = """
            SELECT * FROM accounts.donations
            WHERE user_id = @UserId AND is_deleted IS FALSE
            ORDER BY donated_at DESC
            """;
        var command = new CommandDefinition(
            sql, new { UserId = userId }, Transaction, cancellationToken: cancellationToken);
        var rows = await Connection.QueryAsync<Donation>(command);
        return rows.AsList();
    }

    public async Task<bool> ChangeStatusAsync(
        long donationId,
        string status,
        long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        const string sql = """
            CALL accounts.usp_donation_change_donation_status(
                @donationId, @status, @expectedRowVersion)
            """;
        var command = new CommandDefinition(
            sql,
            new { donationId, status, expectedRowVersion },
            Transaction,
            cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(command);
        return true;
    }
}
