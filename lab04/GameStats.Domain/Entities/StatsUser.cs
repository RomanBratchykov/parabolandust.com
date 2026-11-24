using GameStats.Domain.Common;
using GameStats.Domain.Exceptions;
using GameStats.Domain.ValueObjects;

namespace GameStats.Domain.Entities;

public class StatsUser : BaseEntity
{
    public long UserId { get; private set; }
    public string UserWhoami { get; private set; } = "";     
    public PlayerTotals Totals { get; private set; } = null!;

    private StatsUser() { }

    public StatsUser(long userId, string userWhoami, PlayerTotals totals)
    {
        if (userId <= 0)
            throw new DomainException("UserId must be positive");
        if (string.IsNullOrWhiteSpace(userWhoami))
            throw new DomainException("UserWhoami is required");

        UserId = userId;
        UserWhoami = userWhoami;
        Totals = totals;
        Touch(); 
    }
}