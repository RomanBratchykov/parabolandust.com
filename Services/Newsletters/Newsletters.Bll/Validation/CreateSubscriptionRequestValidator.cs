using FluentValidation;
using Newsletters.Bll.Contracts;

namespace Newsletters.Bll.Validation;

public sealed class CreateSubscriptionRequestValidator : AbstractValidator<CreateSubscriptionRequest>
{
    public CreateSubscriptionRequestValidator()
    {
        RuleFor(request => request.UserId).GreaterThan(0);
        RuleFor(request => request.NewsletterId).GreaterThan(0);
    }
}
