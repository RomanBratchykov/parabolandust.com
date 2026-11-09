using AutoMapper;
using GameStats.Application.Common.Models;
using GameStats.Application.Runs.Commands.CreateRun;
using GameStats.Application.Runs.Commands.DeleteRun;
using GameStats.Application.Runs.Commands.UpdateRun;
using GameStats.Application.Runs.Queries.GetRunById;
using GameStats.Application.Runs.Queries.GetRunsList;
using GameStats.WebApi.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GameStats.WebApi.Controllers;

[ApiController]
[Route("api/runs")]
public class RunsController(ISender mediator, IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<RunDto>>> GetList(
        [FromQuery] long? userId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetRunsListQuery(userId, page, pageSize), ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<RunDto>> GetById(string id, CancellationToken ct)
    {
        var run = await mediator.Send(new GetRunByIdQuery(id), ct);
        Response.Headers.ETag = $"\"{run.Version}\"";   // версія документа
        return Ok(run);
    }

    [HttpPost]
    public async Task<ActionResult<string>> Create(CreateRunRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(mapper.Map<CreateRunCommand>(request), ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id, UpdateRunRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        if (!int.TryParse(ifMatch?.Trim().Trim('"'), out var expectedVersion))
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "If-Match header is required",
                detail: "Pass the document version from the ETag header, e.g. If-Match: \"0\"");

        var newVersion = await mediator.Send(new UpdateRunCommand(
            id, request.EnemiesKilled, request.RoomsVisited, request.TimePlayed,
            request.Successful, expectedVersion), ct);

        Response.Headers.ETag = $"\"{newVersion}\"";
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await mediator.Send(new DeleteRunCommand(id), ct);
        return NoContent();
    }
}