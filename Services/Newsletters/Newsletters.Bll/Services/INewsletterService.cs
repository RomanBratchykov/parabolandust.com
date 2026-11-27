using Newsletters.Bll.Contracts;

namespace Newsletters.Bll.Services;

public interface INewsletterService
{
    Task<PagedResult<NewsletterDto>> GetListAsync(NewsletterQuery query, CancellationToken cancellationToken = default);
    Task<NewsletterDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<NewsletterDetailsDto> GetWithDetailsAsync(long id, CancellationToken cancellationToken = default);
    Task<NewsletterDto> CreateAsync(CreateNewsletterRequest request, CancellationToken cancellationToken = default);
    Task PublishAsync(long id, PublishNewsletterRequest request, CancellationToken cancellationToken = default);
    Task<UserSubscriptionsDto> GetUserSubscriptionsAsync(long userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriberDto>> GetActiveSubscribersAsync(
        long newsletterId, DateTimeOffset since, CancellationToken cancellationToken = default);
}
