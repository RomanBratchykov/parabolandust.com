using AutoMapper;
using Newsletters.Bll.Contracts;
using Newsletters.Bll.Specifications;
using Newsletters.Dal;
using Newsletters.Domain.Exceptions;
using Newsletters.Domain.Models;

namespace Newsletters.Bll.Services;

public sealed class NewsletterService(IUnitOfWork unitOfWork, IMapper mapper) : INewsletterService
{
    public async Task<PagedResult<NewsletterDto>> GetListAsync(
        NewsletterQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var normalized = query with { Page = page, PageSize = pageSize };
        var specification = new NewslettersFilterSpecification(normalized, pageSize);
        var totalCount = await unitOfWork.Newsletters.CountAsync(specification, cancellationToken);
        var newsletters = await unitOfWork.Newsletters.ListAsync(specification, cancellationToken);

        return new PagedResult<NewsletterDto>(mapper.Map<IReadOnlyList<NewsletterDto>>(newsletters),
            totalCount, page, pageSize);
    }

    public async Task<NewsletterDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var newsletter = await unitOfWork.Newsletters.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException($"Newsletter {id} was not found.");
        if (newsletter.IsDeleted)
            throw new EntityNotFoundException($"Newsletter {id} was not found.");
        return mapper.Map<NewsletterDto>(newsletter);
    }

    public async Task<NewsletterDetailsDto> GetWithDetailsAsync(long id, CancellationToken cancellationToken = default)
    {
        var newsletter = await unitOfWork.Newsletters.GetWithDetailsAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException($"Newsletter {id} was not found.");
        if (newsletter.IsDeleted)
            throw new EntityNotFoundException($"Newsletter {id} was not found.");
        return mapper.Map<NewsletterDetailsDto>(newsletter);
    }

    public async Task<NewsletterDto> CreateAsync(
        CreateNewsletterRequest request, CancellationToken cancellationToken = default)
    {
        var admin = await unitOfWork.Admins.GetByIdAsync(request.AdminId, cancellationToken);
        if (admin is null || admin.IsDeleted)
            throw new EntityNotFoundException($"Admin {request.AdminId} was not found.");

        var newsletter = new Newsletter(request.AdminId, request.Title, request.MainInfo, request.ImageUrl);
        await unitOfWork.Newsletters.AddAsync(newsletter, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return mapper.Map<NewsletterDto>(newsletter);
    }

    public async Task PublishAsync(
        long id, PublishNewsletterRequest request, CancellationToken cancellationToken = default)
    {
        var newsletter = await unitOfWork.Newsletters.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException($"Newsletter {id} was not found.");
        if (!newsletter.IsHidden)
            throw new ConflictException($"Newsletter {id} has already been published.");

        newsletter.Publish(request.PublishedBy, DateTimeOffset.UtcNow);
        unitOfWork.Newsletters.Update(newsletter);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserSubscriptionsDto> GetUserSubscriptionsAsync(
        long userId, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.UsersCopy.GetWithSubscriptionsAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException($"User copy {userId} was not found.");
        if (user.IsDeleted)
            throw new EntityNotFoundException($"User copy {userId} was not found.");
        return mapper.Map<UserSubscriptionsDto>(user);
    }

    public async Task<IReadOnlyList<SubscriberDto>> GetActiveSubscribersAsync(
        long newsletterId, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var newsletter = await unitOfWork.Newsletters.GetByIdAsync(newsletterId, cancellationToken);
        if (newsletter is null || newsletter.IsDeleted)
            throw new EntityNotFoundException($"Newsletter {newsletterId} was not found.");

        var users = await unitOfWork.Subscriptions.GetActiveSubscribersAsync(newsletterId, since, cancellationToken);
        return mapper.Map<IReadOnlyList<SubscriberDto>>(users);
    }
}
