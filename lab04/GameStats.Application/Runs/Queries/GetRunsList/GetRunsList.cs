using FluentValidation;
using GameStats.Application.Common.Interfaces;
using GameStats.Application.Common.Models;
using MediatR;

namespace GameStats.Application.Runs.Queries.GetRunsList;

public record GetRunsListQuery(long? UserId, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<RunDto>>;

public class GetRunsListQueryValidator : AbstractValidator<GetRunsListQuery>
{
    public GetRunsListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.UserId).GreaterThan(0).When(x => x.UserId is not null);
    }
}

public class GetRunsListQueryHandler(IRunQueries queries)
    : IRequestHandler<GetRunsListQuery, PagedResult<RunDto>>
{
    public Task<PagedResult<RunDto>> Handle(GetRunsListQuery q, CancellationToken ct)
        => queries.GetListAsync(q.UserId, q.Page, q.PageSize, ct);
}