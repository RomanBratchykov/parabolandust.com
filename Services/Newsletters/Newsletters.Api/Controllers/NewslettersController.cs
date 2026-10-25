using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Newsletters.Bll.Contracts;
using Newsletters.Bll.Services;

namespace Newsletters.Api.Controllers;

[ApiController]
[Route("api/newsletters")]
public sealed class NewslettersController(INewsletterService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NewsletterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<NewsletterDto>>> GetList(
        [FromQuery] NewsletterQuery query,
        [FromServices] IValidator<NewsletterQuery> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new ValidationProblemDetails(validation.ToErrorDictionary()));

        return Ok(await service.GetListAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(NewsletterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NewsletterDto>> GetById(long id, CancellationToken cancellationToken)
        => Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpGet("{id:long}/details")]
    [ProducesResponseType(typeof(NewsletterDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NewsletterDetailsDto>> GetWithDetails(long id, CancellationToken cancellationToken)
        => Ok(await service.GetWithDetailsAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(NewsletterDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NewsletterDto>> Create(
        CreateNewsletterRequest request,
        [FromServices] IValidator<CreateNewsletterRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new ValidationProblemDetails(validation.ToErrorDictionary()));

        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:long}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(
        long id,
        PublishNewsletterRequest request,
        [FromServices] IValidator<PublishNewsletterRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new ValidationProblemDetails(validation.ToErrorDictionary()));

        await service.PublishAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:long}/subscribers")]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SubscriberDto>>> GetSubscribers(
        long id, [FromQuery] DateTimeOffset? since, CancellationToken cancellationToken)
        => Ok(await service.GetActiveSubscribersAsync(id, since ?? DateTimeOffset.MinValue, cancellationToken));
}
