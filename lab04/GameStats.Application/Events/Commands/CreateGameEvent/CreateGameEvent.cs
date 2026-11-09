using System.Text.Json;
using FluentValidation;
using GameStats.Application.Common.Interfaces;
using GameStats.Domain.Entities;
using GameStats.Domain.Interfaces;
using MediatR;
using MongoDB.Bson;

namespace GameStats.Application.Events.Commands.CreateGameEvent;

public record CreateGameEventCommand(
    string EventId, string EventType, long UserId, string? RunId, JsonElement Payload)
    : ICommand<string>;

public class CreateGameEventCommandValidator : AbstractValidator<CreateGameEventCommand>
{
    public CreateGameEventCommandValidator(IGameEventRepository events)
    {
        RuleFor(x => x.EventId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EventType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.Payload.ValueKind)
            .Equal(JsonValueKind.Object).WithMessage("Payload must be a JSON object");

        RuleFor(x => x.EventId)
            .MustAsync(async (id, ct) => !await events.ExistsByEventIdAsync(id, ct))
            .WithMessage("An event with this EventId already exists")
            .When(x => !string.IsNullOrWhiteSpace(x.EventId));
    }
}

public class CreateGameEventCommandHandler(IGameEventRepository events)
    : IRequestHandler<CreateGameEventCommand, string>
{
    public async Task<string> Handle(CreateGameEventCommand cmd, CancellationToken ct)
    {
        var payload = BsonDocument.Parse(cmd.Payload.GetRawText());
        var gameEvent = new GameEvent(cmd.EventId, cmd.EventType, cmd.UserId, cmd.RunId, payload);

        await events.AddAsync(gameEvent, ct);
        return gameEvent.Id.ToString();
    }
}