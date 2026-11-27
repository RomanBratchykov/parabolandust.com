using Accounts.Bll;
using Accounts.Bll.Contracts;
using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults;

namespace Accounts.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IAccountsService accountsService, BusinessMetrics metrics) : ControllerBase
{
    [HttpGet("{userId:long}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> Get(long userId, CancellationToken cancellationToken)
    {
        var user = await accountsService.GetUserAsync(userId, cancellationToken);
        return user is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Resource not found", detail: $"User {userId} was not found.")
            : Ok(user);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        using var operation = metrics.Start("account.create");
        var userId = await accountsService.CreateUserAsync(request, cancellationToken);
        operation.Succeed();
        return CreatedAtAction(nameof(Get), new { userId }, new { userId });
    }

    [HttpPut("{userId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(long userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await accountsService.UpdateUserAsync(userId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{userId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long userId, [FromQuery] long expectedRowVersion, CancellationToken cancellationToken)
    {
        await accountsService.DeleteUserAsync(userId, expectedRowVersion, cancellationToken);
        return NoContent();
    }

    [HttpPut("{userId:long}/identity")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RotateIdentity(long userId, RotateIdentityRequest request, CancellationToken cancellationToken)
    {
        await accountsService.RotateIdentityAsync(userId, request, cancellationToken);
        return NoContent();
    }
}
