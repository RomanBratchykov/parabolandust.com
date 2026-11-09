using FluentValidation;
using GameStats.Application.Common.Interfaces;
using GameStats.Application.Common.Models;
using MediatR;

namespace GameStats.Application.Stats.Queries.GetLeaderboard;

public record GetLeaderboardQuery(int Top = 10) : IQuery<IReadOnlyList<LeaderboardEntryDto>>;

public class GetLeaderboardQueryValidator : AbstractValidator<GetLeaderboardQuery>
{
    public GetLeaderboardQueryValidator() => RuleFor(x => x.Top).InclusiveBetween(1, 100);
}

public class GetLeaderboardQueryHandler(IStatsQueries queries)
    : IRequestHandler<GetLeaderboardQuery, IReadOnlyList<LeaderboardEntryDto>>
{
    public Task<IReadOnlyList<LeaderboardEntryDto>> Handle(GetLeaderboardQuery q, CancellationToken ct)
        => queries.GetLeaderboardAsync(q.Top, ct);
}