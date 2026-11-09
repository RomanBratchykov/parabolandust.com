using AutoMapper;
using GameStats.Application.Common.Models;
using GameStats.Application.Events.Commands.CreateGameEvent;
using GameStats.Application.Events.Queries.GetEventCounts;
using GameStats.WebApi.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GameStats.WebApi.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController(ISender mediator, IMapper mapper) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateGameEventRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(mapper.Map<CreateGameEventCommand>(request), ct);
        return StatusCode(StatusCodes.Status201Created, new { id });
    }

    [HttpGet("counts")]
    public async Task<ActionResult<IReadOnlyList<EventCountDto>>> Counts(
        [FromQuery] long userId, CancellationToken ct)
        => Ok(await mediator.Send(new GetEventCountsQuery(userId), ct));
}