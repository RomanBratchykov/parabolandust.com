using System.ComponentModel.DataAnnotations;

namespace Newsletters.Bll.Contracts;

public sealed record NewsletterDto(
    long Id,
    long AdminId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    string Title,
    string MainInfo,
    string? ImageUrl,
    bool IsHidden,
    string? PublishedBy);

public sealed record NewsletterDetailsDto(
    NewsletterDto Newsletter,
    NewsletterDeliverySettingsDto? DeliverySettings,
    IReadOnlyList<UserCommentDto> Comments,
    IReadOnlyList<SubscriptionDto> Subscriptions);

public sealed record NewsletterDeliverySettingsDto(string RecipientSettings, string? ReplyToEmail);

public sealed record UserCommentDto(long Id, long UserId, string UserName, string CommentText,
    DateTimeOffset CreatedAt, bool IsHidden);

public sealed record CreateNewsletterRequest(
    [Range(1, long.MaxValue)] long AdminId,
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [Required, MinLength(1)] string MainInfo,
    [StringLength(2048)] string? ImageUrl);

public sealed record PublishNewsletterRequest(
    [Required, StringLength(100, MinimumLength = 1)] string PublishedBy);

public sealed record NewsletterQuery(
    [Range(1, int.MaxValue)] int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    [StringLength(200)] string? Search = null,
    long? AdminId = null,
    bool? IsHidden = null,
    DateTimeOffset? PublishedAfter = null,
    string SortBy = "createdAt",
    string SortDir = "desc");

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}

public sealed record UserSubscriptionsDto(long Id, string UserName, string? Email,
    IReadOnlyList<UserSubscriptionDto> Subscriptions);

public sealed record UserSubscriptionDto(long Id, long NewsletterId, string NewsletterTitle,
    string Status, DateTimeOffset ConsentAt);

public sealed record SubscriberDto(long Id, string UserName, string? Email);

public sealed record CreateSubscriptionRequest(
    [Range(1, long.MaxValue)] long UserId,
    [Range(1, long.MaxValue)] long NewsletterId);

public sealed record SubscriptionDto(
    long Id,
    long UserId,
    long NewsletterId,
    string Status,
    DateTimeOffset ConsentAt,
    string? UserEmailSnapshot);
