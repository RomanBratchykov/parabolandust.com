using Microsoft.EntityFrameworkCore;
using Newsletters.Dal.Data;
using Newsletters.Domain.Models;

namespace Newsletters.Dal.Repositories;

public sealed class NewsletterRepository(NewslettersDbContext context)
    : GenericRepository<Newsletter>(context), INewsletterRepository
{
    public Task<Newsletter?> GetWithDetailsAsync(long id, CancellationToken cancellationToken = default)
        => Set.Include(newsletter => newsletter.DeliverySettings)
            .Include(newsletter => newsletter.Comments).ThenInclude(comment => comment.User)
            .Include(newsletter => newsletter.Subscriptions).ThenInclude(subscription => subscription.User)
            .AsSingleQuery()
            .FirstOrDefaultAsync(newsletter => newsletter.Id == id, cancellationToken);
}
