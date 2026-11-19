using System.Reflection;
using RabbitMQ.Client;

namespace Platform.Messaging.Tests;

internal sealed record BrokerCall(string Method, IReadOnlyDictionary<string, object?> Arguments);

// Exercise the real RabbitMQ interfaces without replacing production broker operations
// with a second abstraction. Unexpected calls fail instead of silently doing nothing.
public class RecordingProxy<T> : DispatchProxy where T : class
{
    internal Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args!);

    internal static T Create(Func<MethodInfo, object?[], object?> handler)
    {
        var proxy = Create<T, RecordingProxy<T>>();
        ((RecordingProxy<T>)(object)proxy).Handler = handler;
        return proxy;
    }
}

internal sealed class BrokerDouble
{
    internal List<BrokerCall> Calls { get; } = [];
    internal TaskCompletionSource Confirmation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Exception? DeclarationFailure { get; set; }
    internal CreateChannelOptions? ChannelOptions { get; private set; }
    internal CancellationToken ChannelToken { get; private set; }
    internal IChannel Channel { get; }
    internal IConnection Connection { get; }

    internal BrokerDouble()
    {
        Channel = RecordingProxy<IChannel>.Create((method, args) =>
        {
            Calls.Add(new BrokerCall(method.Name, method.GetParameters()
                .Select((parameter, index) => (parameter.Name!, args[index]))
                .ToDictionary(pair => pair.Item1, pair => pair.Item2)));
            return method.Name switch
            {
                "ExchangeDeclareAsync" when DeclarationFailure is not null => Task.FromException(DeclarationFailure),
                "ExchangeDeclareAsync" or "QueueBindAsync" => Task.CompletedTask,
                "QueueDeclareAsync" => Task.FromResult(new QueueDeclareOk((string)args[0]!, 0, 0)),
                "BasicPublishAsync" => new ValueTask(Confirmation.Task.WaitAsync((CancellationToken)args[^1]!)),
                "DisposeAsync" => ValueTask.CompletedTask,
                _ => throw new NotSupportedException(method.Name)
            };
        });
        Connection = RecordingProxy<IConnection>.Create((method, args) =>
        {
            if (method.Name != "CreateChannelAsync") throw new NotSupportedException(method.Name);
            ChannelOptions = (CreateChannelOptions?)args[0];
            ChannelToken = (CancellationToken)args[1]!;
            return Task.FromResult(Channel);
        });
    }
}
