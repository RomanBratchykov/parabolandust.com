using FluentValidation;
using Newsletters.Bll.Contracts;

namespace Newsletters.Bll.Validation;

public sealed class PublishNewsletterRequestValidator : AbstractValidator<PublishNewsletterRequest>
{
    public PublishNewsletterRequestValidator()
        => RuleFor(request => request.PublishedBy).NotEmpty().MaximumLength(100);
}
