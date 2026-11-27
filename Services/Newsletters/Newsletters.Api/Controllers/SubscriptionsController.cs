using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Newsletters.Bll.Contracts;
using Newsletters.Bll.Services;

namespace Newsletters.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
public sealed class SubscriptionsController(ISubscriptionService service) : ControllerBase
{
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionDto>> GetById(long id, CancellationToken cancellationToken)
        => Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubscriptionDto>> Create(
        CreateSubscriptionRequest request,
        [FromServices] IValidator<CreateSubscriptionRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new ValidationProblemDetails(validation.ToErrorDictionary()));

        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
