using Microsoft.EntityFrameworkCore;
using Newsletters.Dal.Data;
using Newsletters.Domain.Models;

namespace Newsletters.Dal.Repositories;

public sealed class UserCopyRepository(NewslettersDbContext context)
    : GenericRepository<UserCopy>(context), IUserCopyRepository
{
    public async Task<UserCopy?> GetWithSubscriptionsAsync(long id, CancellationToken cancellationToken = default)
    {
        var user = await Set.FirstOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (user is null)
            return null;

        await Context.Entry(user).Collection(value => value.Subscriptions)
            .Query().Include(subscription => subscription.Newsletter).LoadAsync(cancellationToken);
        return user;
    }
}
