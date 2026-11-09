using FluentValidation;
using GameStats.Application.Common.Interfaces;
using GameStats.Application.Common.Models;
using GameStats.Domain.Exceptions;
using MediatR;
using MongoDB.Bson;

namespace GameStats.Application.Runs.Queries.GetRunById;

public record GetRunByIdQuery(string Id) : IQuery<RunDto>;

public class GetRunByIdQueryValidator : AbstractValidator<GetRunByIdQuery>
{
    public GetRunByIdQueryValidator()
        => RuleFor(x => x.Id).Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("Id must be a valid ObjectId");
}

public class GetRunByIdQueryHandler(IRunQueries queries)
    : IRequestHandler<GetRunByIdQuery, RunDto>
{
    public async Task<RunDto> Handle(GetRunByIdQuery q, CancellationToken ct)
        => await queries.GetByIdAsync(q.Id, ct)
           ?? throw new NotFoundException($"Run {q.Id} not found");
}