using AutoMapper;
using Newsletters.Bll.Contracts;
using Newsletters.Dal;
using Newsletters.Domain.Exceptions;
using Newsletters.Domain.Models;

namespace Newsletters.Bll.Services;

public sealed class SubscriptionService(IUnitOfWork unitOfWork, IMapper mapper) : ISubscriptionService
{
    public async Task<SubscriptionDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var subscription = await unitOfWork.Subscriptions.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException($"Subscription {id} was not found.");
        if (subscription.IsDeleted)
            throw new EntityNotFoundException($"Subscription {id} was not found.");
        return mapper.Map<SubscriptionDto>(subscription);
    }

    public async Task<SubscriptionDto> CreateAsync(
        CreateSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.UsersCopy.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new EntityNotFoundException($"User copy {request.UserId} was not found.");
        if (user.IsDeleted)
            throw new EntityNotFoundException($"User copy {request.UserId} was not found.");
        var newsletter = await unitOfWork.Newsletters.GetByIdAsync(request.NewsletterId, cancellationToken)
            ?? throw new EntityNotFoundException($"Newsletter {request.NewsletterId} was not found.");
        if (newsletter.IsHidden || newsletter.IsDeleted)
            throw new ConflictException("Only visible newsletters can be subscribed to.");
        if (await unitOfWork.Subscriptions.GetByUserAndNewsletterAsync(
                request.UserId, request.NewsletterId, cancellationToken) is not null)
            throw new ConflictException("The user is already subscribed to this newsletter.");

        var subscription = new Subscription(user.Id, newsletter.Id, user.Email);
        await unitOfWork.Subscriptions.AddAsync(subscription, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return mapper.Map<SubscriptionDto>(subscription);
    }
}
