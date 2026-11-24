using FluentValidation;
using GameStats.Application.Common.Interfaces;
using GameStats.Application.Common.Models;
using MediatR;

namespace GameStats.Application.Events.Queries.GetEventCounts;

public record GetEventCountsQuery(long UserId) : IQuery<IReadOnlyList<EventCountDto>>;

public class GetEventCountsQueryValidator : AbstractValidator<GetEventCountsQuery>
{
    public GetEventCountsQueryValidator() => RuleFor(x => x.UserId).GreaterThan(0);
}

public class GetEventCountsQueryHandler(IGameEventQueries queries)
    : IRequestHandler<GetEventCountsQuery, IReadOnlyList<EventCountDto>>
{
    public Task<IReadOnlyList<EventCountDto>> Handle(GetEventCountsQuery q, CancellationToken ct)
        => queries.GetCountsByTypeAsync(q.UserId, ct);
}