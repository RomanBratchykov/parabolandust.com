using FluentValidation;
using GameStats.Application.Common.Interfaces;
using GameStats.Domain.Entities;
using GameStats.Domain.Interfaces;
using GameStats.Domain.ValueObjects;
using MediatR;

namespace GameStats.Application.Runs.Commands.CreateRun;

public record CreateRunCommand(
    string RunId, long UserId, string UserName, string UserWhoami, string GameVersion,
    int EnemiesKilled, int RoomsVisited, int TimePlayed) : ICommand<string>;

public class CreateRunCommandValidator : AbstractValidator<CreateRunCommand>
{
    public CreateRunCommandValidator(IRunRepository runs)
    {
        RuleFor(x => x.RunId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.UserWhoami).NotEmpty().MaximumLength(128);
        RuleFor(x => x.GameVersion).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EnemiesKilled).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RoomsVisited).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TimePlayed).GreaterThanOrEqualTo(0);

        RuleFor(x => x.RunId)
            .MustAsync(async (runId, ct) => !await runs.ExistsByRunIdAsync(runId, ct))
            .WithMessage("A run with this RunId already exists")
            .When(x => !string.IsNullOrWhiteSpace(x.RunId));
    }
}

public class CreateRunCommandHandler(IRunRepository runs)
    : IRequestHandler<CreateRunCommand, string>
{
    public async Task<string> Handle(CreateRunCommand cmd, CancellationToken ct)
    {
        var run = new Run(
            cmd.RunId, cmd.UserId, cmd.GameVersion,
            new RunStats(cmd.EnemiesKilled, cmd.RoomsVisited, cmd.TimePlayed),
            new PlayerSnapshot(cmd.UserName, cmd.UserWhoami));

        await runs.AddAsync(run, ct);
        return run.Id.ToString();
    }
}