using Microsoft.AspNetCore.Mvc;
using Newsletters.Bll.Contracts;
using Newsletters.Bll.Services;

namespace Newsletters.Api.Controllers;

[ApiController]
[Route("api/users-copy")]
public sealed class UserCopiesController(INewsletterService service) : ControllerBase
{
    [HttpGet("{userId:long}/subscriptions")]
    [ProducesResponseType(typeof(UserSubscriptionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserSubscriptionsDto>> GetSubscriptions(
        long userId, CancellationToken cancellationToken)
        => Ok(await service.GetUserSubscriptionsAsync(userId, cancellationToken));
}
