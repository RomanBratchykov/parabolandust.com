using GameStats.Application.Common.Models;
using GameStats.Application.Stats.Queries.GetLeaderboard;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GameStats.WebApi.Controllers;

[ApiController]
[Route("api/stats")]
public class StatsController(ISender mediator) : ControllerBase
{
    [HttpGet("leaderboard")]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntryDto>>> Leaderboard(
        [FromQuery] int top = 10, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetLeaderboardQuery(top), ct));
}