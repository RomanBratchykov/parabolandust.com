using Newsletters.Domain.Models;

namespace Newsletters.Dal.Repositories;

public interface INewsletterRepository : IGenericRepository<Newsletter>
{
    Task<Newsletter?> GetWithDetailsAsync(long id, CancellationToken cancellationToken = default);
}
