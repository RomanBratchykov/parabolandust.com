using FluentValidation;
using GameStats.Application.Common.Interfaces;
using GameStats.Domain.Exceptions;
using GameStats.Domain.Interfaces;
using MediatR;
using MongoDB.Bson;

namespace GameStats.Application.Runs.Commands.DeleteRun;

public record DeleteRunCommand(string Id) : ICommand;

public class DeleteRunCommandValidator : AbstractValidator<DeleteRunCommand>
{
    public DeleteRunCommandValidator()
        => RuleFor(x => x.Id).Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("Id must be a valid ObjectId");
}

public class DeleteRunCommandHandler(IRunRepository runs) : IRequestHandler<DeleteRunCommand>
{
    public async Task Handle(DeleteRunCommand cmd, CancellationToken ct)
    {
        var deleted = await runs.DeleteAsync(ObjectId.Parse(cmd.Id), ct);
        if (!deleted)
            throw new NotFoundException($"Run {cmd.Id} not found");
    }
}