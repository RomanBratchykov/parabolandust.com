using GameStats.Domain.Common;
using GameStats.Domain.Exceptions;

namespace GameStats.Domain.ValueObjects;

public sealed class PlayerSnapshot : ValueObject
{
    public string UserName { get; }
    public string UserWhoami { get; }

    public PlayerSnapshot(string userName, string userWhoami)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new DomainException("UserName is required");
        if (string.IsNullOrWhiteSpace(userWhoami))
            throw new DomainException("UserWhoami is required");

        UserName = userName;
        UserWhoami = userWhoami;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserName;
        yield return UserWhoami;
    }
}