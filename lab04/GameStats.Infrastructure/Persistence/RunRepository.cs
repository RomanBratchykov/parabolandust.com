using GameStats.Domain.Entities;
using GameStats.Domain.Exceptions;
using GameStats.Domain.Interfaces;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GameStats.Infrastructure.Persistence;

public class RunRepository(MongoDbContext context) : IRunRepository
{
    private readonly IMongoCollection<Run> _runs = context.Runs;

    public async Task<Run?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _runs.Find(r => r.Id == id).FirstOrDefaultAsync(ct);

    public Task<bool> ExistsByRunIdAsync(string runId, CancellationToken ct = default)
        => _runs.Find(r => r.RunId == runId).AnyAsync(ct);

    // дублікат run_id -> MongoWriteException(DuplicateKey) -> 409 в обробнику помилок
    public Task AddAsync(Run run, CancellationToken ct = default)
        => _runs.InsertOneAsync(run, cancellationToken: ct);

    public async Task UpdateAsync(Run run, CancellationToken ct = default)
    {
        var expectedVersion = run.Version;
        run.BumpVersion();

        // оптимістична конкурентність: заміна лише документа з очікуваною версією
        var result = await _runs.ReplaceOneAsync(
            r => r.Id == run.Id && r.Version == expectedVersion,
            run, cancellationToken: ct);

        if (result.MatchedCount == 0)
            throw new ConflictException($"Run {run.Id} was modified");
    }

    public async Task<bool> DeleteAsync(ObjectId id, CancellationToken ct = default)
    {
        var result = await _runs.DeleteOneAsync(r => r.Id == id, ct);
        return result.DeletedCount > 0;
    }
}