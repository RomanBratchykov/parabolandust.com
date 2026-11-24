using GameStats.Domain.Common;
using GameStats.Domain.Exceptions;

namespace GameStats.Domain.ValueObjects;

public sealed class PlayerTotals : ValueObject
{
    public int TimesKilled { get; }
    public int TimesWon { get; }
    public long TimePlayed { get; }

    public PlayerTotals(int timesKilled, int timesWon, long timePlayed)
    {
        if (timesKilled < 0 || timesWon < 0 || timePlayed < 0)
            throw new DomainException("Totals cannot be negative");

        TimesKilled = timesKilled;
        TimesWon = timesWon;
        TimePlayed = timePlayed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TimesKilled;
        yield return TimesWon;
        yield return TimePlayed;
    }
}