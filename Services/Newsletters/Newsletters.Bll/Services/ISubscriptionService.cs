using Newsletters.Bll.Contracts;

namespace Newsletters.Bll.Services;

public interface ISubscriptionService
{
    Task<SubscriptionDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<SubscriptionDto> CreateAsync(CreateSubscriptionRequest request, CancellationToken cancellationToken = default);
}
