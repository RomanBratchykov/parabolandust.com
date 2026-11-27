using Microsoft.EntityFrameworkCore;
using Newsletters.Dal.Data;
using Newsletters.Domain.Specifications;

namespace Newsletters.Dal.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected NewslettersDbContext Context { get; }
    protected DbSet<T> Set { get; }

    public GenericRepository(NewslettersDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await Set.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync(
        ISpecification<T> specification, CancellationToken cancellationToken = default)
        => await SpecificationEvaluator.Apply(Set.AsNoTracking(), specification).ToListAsync(cancellationToken);

    public async Task<int> CountAsync(
        ISpecification<T> specification, CancellationToken cancellationToken = default)
        => await SpecificationEvaluator.Apply(Set.AsNoTracking(), specification, applyPaging: false,
            applyIncludes: false, applyOrdering: false).CountAsync(cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await Set.AddAsync(entity, cancellationToken);

    public void Update(T entity) => Set.Update(entity);
    public void Delete(T entity) => Set.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Context.SaveChangesAsync(cancellationToken);
}
