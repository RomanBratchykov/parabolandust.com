using FluentValidation;
using GameStats.Application.Common.Interfaces;
using GameStats.Domain.Exceptions;
using GameStats.Domain.Interfaces;
using GameStats.Domain.ValueObjects;
using MediatR;
using MongoDB.Bson;

namespace GameStats.Application.Runs.Commands.UpdateRun;

// Successful = null -> лише оновити статистику; true/false -> ще й завершити забіг
public record UpdateRunCommand(
    string Id, int EnemiesKilled, int RoomsVisited, int TimePlayed,
    bool? Successful, int ExpectedVersion) : ICommand<int>;

public class UpdateRunCommandValidator : AbstractValidator<UpdateRunCommand>
{
    public UpdateRunCommandValidator()
    {
        RuleFor(x => x.Id).Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("Id must be a valid ObjectId");
        RuleFor(x => x.EnemiesKilled).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RoomsVisited).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TimePlayed).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public class UpdateRunCommandHandler(IRunRepository runs)
    : IRequestHandler<UpdateRunCommand, int>
{
    public async Task<int> Handle(UpdateRunCommand cmd, CancellationToken ct)
    {
        var run = await runs.GetByIdAsync(ObjectId.Parse(cmd.Id), ct)
                  ?? throw new NotFoundException($"Run {cmd.Id} not found");

        if (run.Version != cmd.ExpectedVersion)
            throw new ConflictException($"Run {cmd.Id} was modified; reload before updating");

        run.UpdateStats(new RunStats(cmd.EnemiesKilled, cmd.RoomsVisited, cmd.TimePlayed));
        if (cmd.Successful is not null)
            run.Finish(cmd.Successful.Value);

        await runs.UpdateAsync(run, ct);   // тут ще раз перевіряється Version на рівні БД
        return run.Version;
    }
}