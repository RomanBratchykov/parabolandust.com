using Accounts.Bll;
using Accounts.Bll.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Accounts.Api.Controllers;

[ApiController]
[Route("api/user-achievements")]
public sealed class UserAchievementsController(IAccountsService accountsService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Grant(GrantAchievementRequest request, CancellationToken cancellationToken)
    {
        await accountsService.GrantAchievementAsync(request, cancellationToken);
        return NoContent();
    }
}
