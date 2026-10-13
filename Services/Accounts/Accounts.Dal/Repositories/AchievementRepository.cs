using Accounts.Domain.Models;
using Npgsql;

namespace Accounts.Dal.Repositories;

public sealed class AchievementRepository : BaseDapperRepository<Achievement>, IAchievementRepository
{
    public AchievementRepository(NpgsqlConnection connection, Func<NpgsqlTransaction?> currentTransaction)
        : base(
            connection,
            currentTransaction,
            "accounts.achievement",
            "achievement_id",
            """
            INSERT INTO accounts.achievement
                (achievement_name, achievement_description, is_secret)
            VALUES
                (@AchievementName, @AchievementDescription, @IsSecret)
            RETURNING achievement_id
            """,
            achievement => new
            {
                achievement.AchievementName,
                achievement.AchievementDescription,
                achievement.IsSecret
            },
            """
            UPDATE accounts.achievement
            SET achievement_name = @AchievementName,
                achievement_description = @AchievementDescription,
                is_secret = @IsSecret
            WHERE achievement_id = @AchievementId
            """,
            achievement => new
            {
                achievement.AchievementId,
                achievement.AchievementName,
                achievement.AchievementDescription,
                achievement.IsSecret
            },
            "DELETE FROM accounts.achievement WHERE achievement_id = @Id")
    {
    }
}
