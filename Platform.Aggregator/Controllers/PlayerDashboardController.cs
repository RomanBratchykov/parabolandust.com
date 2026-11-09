using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Contracts;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

[ApiController]
[Route("api/aggregator/users")]
public sealed class PlayerDashboardController(PlayerDashboardService dashboards) : ControllerBase
{
    [HttpGet("{userId:long}/dashboard")]
    [ProducesResponseType<PlayerDashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<PlayerDashboardDto>> GetDashboard(
        [Range(1, long.MaxValue)] long userId, CancellationToken cancellationToken)
    {
        try
        {
            var dashboard = await dashboards.GetDashboardAsync(userId, cancellationToken);
            return dashboard is null
                ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Player not found.")
                : Ok(dashboard);
        }
        catch (DashboardUnavailableException exception)
        {
            return Problem(statusCode: exception.TimedOut
                    ? StatusCodes.Status504GatewayTimeout : StatusCodes.Status503ServiceUnavailable,
                title: exception.Message);
        }
    }
}
