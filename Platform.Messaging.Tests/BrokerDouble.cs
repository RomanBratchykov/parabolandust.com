using System.Reflection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

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
    internal Exception? AckFailure { get; set; }
    internal TaskCompletionSource<IAsyncBasicConsumer> Subscribed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource ChannelDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal AsyncEventHandler<ShutdownEventArgs>? Shutdown { get; private set; }
    internal CreateChannelOptions? ChannelOptions { get; private set; }
    internal CancellationToken ChannelToken { get; private set; }
    internal IChannel Channel { get; }
    internal IConnection Connection { get; }
    internal bool IsOpen { get; set; } = true;

    internal BrokerDouble()
    {
        Channel = RecordingProxy<IChannel>.Create((method, args) =>
        {
            Calls.Add(new BrokerCall(method.Name, method.GetParameters()
                .Select((parameter, index) => (parameter.Name!, args[index]))
                .ToDictionary(pair => pair.Item1, pair => pair.Item2)));
            if (method.Name == "add_ChannelShutdownAsync")
            {
                Shutdown += (AsyncEventHandler<ShutdownEventArgs>)args[0]!;
                return null;
            }
            if (method.Name == "remove_ChannelShutdownAsync")
            {
                Shutdown -= (AsyncEventHandler<ShutdownEventArgs>)args[0]!;
                return null;
            }
            if (method.Name == "BasicConsumeAsync")
            {
                Subscribed.TrySetResult((IAsyncBasicConsumer)args[6]!);
                return Task.FromResult("test-consumer");
            }
            if (method.Name == "DisposeAsync") ChannelDisposed.TrySetResult();
            return method.Name switch
            {
                "ExchangeDeclareAsync" when DeclarationFailure is not null => Task.FromException(DeclarationFailure),
                "ExchangeDeclareAsync" or "QueueBindAsync" or "BasicQosAsync" => Task.CompletedTask,
                "QueueDeclareAsync" => Task.FromResult(new QueueDeclareOk((string)args[0]!, 0, 0)),
                "BasicPublishAsync" => new ValueTask(Confirmation.Task.WaitAsync((CancellationToken)args[^1]!)),
                "BasicAckAsync" when AckFailure is not null => ValueTask.FromException(AckFailure),
                "BasicAckAsync" or "BasicNackAsync" => ValueTask.CompletedTask,
                "DisposeAsync" => ValueTask.CompletedTask,
                _ => throw new NotSupportedException(method.Name)
            };
        });
        Connection = RecordingProxy<IConnection>.Create((method, args) =>
        {
            if (method.Name == "get_IsOpen") return IsOpen;
            if (method.Name is "Dispose" or "CloseAsync") return method.ReturnType == typeof(Task) ? Task.CompletedTask : null;
            if (method.Name != "CreateChannelAsync") throw new NotSupportedException(method.Name);
            ChannelOptions = (CreateChannelOptions?)args[0];
            ChannelToken = (CancellationToken)args[1]!;
            return Task.FromResult(Channel);
        });
    }
}
