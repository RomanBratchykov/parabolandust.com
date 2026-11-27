using FluentValidation;
using Newsletters.Bll.Contracts;

namespace Newsletters.Bll.Validation;

public sealed class CreateNewsletterRequestValidator : AbstractValidator<CreateNewsletterRequest>
{
    public CreateNewsletterRequestValidator()
    {
        RuleFor(request => request.AdminId).GreaterThan(0);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.MainInfo).NotEmpty();
        RuleFor(request => request.ImageUrl).MaximumLength(2048);
    }
}
