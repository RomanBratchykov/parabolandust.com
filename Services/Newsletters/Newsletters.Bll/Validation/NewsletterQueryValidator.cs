using FluentValidation;
using Newsletters.Bll.Contracts;

namespace Newsletters.Bll.Validation;

public sealed class NewsletterQueryValidator : AbstractValidator<NewsletterQuery>
{
    private static readonly string[] SortFields = ["createdAt", "publishedAt", "title"];

    public NewsletterQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query).Must(query => (long)(query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset is too large.");
        RuleFor(query => query.AdminId).GreaterThan(0).When(query => query.AdminId.HasValue);
        RuleFor(query => query.Search).MaximumLength(200);
        RuleFor(query => query.SortBy).Must(value => SortFields.Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortBy must be createdAt, publishedAt, or title.");
        RuleFor(query => query.SortDir).Must(value => !string.IsNullOrWhiteSpace(value)
            && (value.Equals("asc", StringComparison.OrdinalIgnoreCase)
                || value.Equals("desc", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("SortDir must be asc or desc.");
    }
}
