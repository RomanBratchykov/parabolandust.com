using GameStats.Domain.Common;
using GameStats.Domain.Exceptions;

namespace GameStats.Domain.ValueObjects;

public sealed class RunStats : ValueObject
{
    public int EnemiesKilled { get; }
    public int RoomsVisited { get; }
    public int TimePlayed { get; }

    public RunStats(int enemiesKilled, int roomsVisited, int timePlayed)
    {
        if (enemiesKilled < 0 || roomsVisited < 0 || timePlayed < 0)
            throw new DomainException("Run stats cannot be negative");

        EnemiesKilled = enemiesKilled;
        RoomsVisited = roomsVisited;
        TimePlayed = timePlayed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EnemiesKilled;
        yield return RoomsVisited;
        yield return TimePlayed;
    }
}