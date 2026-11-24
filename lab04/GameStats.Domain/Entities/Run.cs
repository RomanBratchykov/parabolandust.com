using GameStats.Domain.Common;
using GameStats.Domain.Exceptions;
using GameStats.Domain.ValueObjects;

namespace GameStats.Domain.Entities;

public class Run : BaseEntity
{
    public string RunId { get; private set; } = "";         
    public long UserId { get; private set; }                
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public string GameVersion { get; private set; } = "";
    public bool? Successful { get; private set; }
    public RunStats CurrentStats { get; private set; } = null!;       
    public PlayerSnapshot? PlayerSnapshot { get; private set; }     

    private Run() { } 

    public Run(string runId, long userId, string gameVersion,
               RunStats currentStats, PlayerSnapshot? playerSnapshot)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new DomainException("RunId is required");
        if (userId <= 0)
            throw new DomainException("UserId must be positive");
        if (string.IsNullOrWhiteSpace(gameVersion))
            throw new DomainException("GameVersion is required");

        RunId = runId;
        UserId = userId;
        GameVersion = gameVersion;
        CurrentStats = currentStats;
        PlayerSnapshot = playerSnapshot;
        StartedAt = DateTime.UtcNow;
    }

    public void UpdateStats(RunStats stats)
    {
        if (EndedAt is not null)
            throw new DomainException("Cannot update a finished run");

        CurrentStats = stats;
        Touch();
    }

    public void Finish(bool successful)
    {
        if (EndedAt is not null)
            throw new DomainException("Run is already finished");

        EndedAt = DateTime.UtcNow;
        Successful = successful;
        Touch();
    }
}