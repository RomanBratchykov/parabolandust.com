using GameStats.Domain.Entities;
using MongoDB.Bson;

namespace GameStats.Domain.Interfaces;

public interface IRunRepository
{
    Task<Run?> GetByIdAsync(ObjectId id, CancellationToken ct = default);
    Task<bool> ExistsByRunIdAsync(string runId, CancellationToken ct = default);
    Task AddAsync(Run run, CancellationToken ct = default);
    Task UpdateAsync(Run run, CancellationToken ct = default);   
    Task<bool> DeleteAsync(ObjectId id, CancellationToken ct = default);
}