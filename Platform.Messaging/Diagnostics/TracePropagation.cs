using System.Diagnostics;
using System.Text;
using OpenTelemetry.Context.Propagation;

namespace Platform.Messaging.Diagnostics;

public static class TracePropagation
{
    public const string SourceName = "Platform.Messaging";
    public const string CorrelationHeader = "x-correlation-id";
    public static readonly ActivitySource Source = new(SourceName);
    // Only trace context crosses this boundary; arbitrary request baggage stays local.
    private static readonly TraceContextPropagator Propagator = new();

    public static void Inject(IDictionary<string, object?> headers, string? correlationId)
    {
        foreach (var key in new[] { "traceparent", "tracestate", "baggage", CorrelationHeader })
            headers.Remove(key);
        if (Activity.Current is { } activity)
            Propagator.Inject(new PropagationContext(activity.Context, default), headers,
                (carrier, key, value) => carrier[key] = value);
        if (!string.IsNullOrWhiteSpace(correlationId))
            headers[CorrelationHeader] = correlationId;
    }

    public static string? ReadCorrelationId(IDictionary<string, object?>? headers) =>
        ReadHeader(headers, CorrelationHeader).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    public static Activity? StartProducerActivity(string exchange, string eventType, string messageId) =>
        Tag(Source.StartActivity($"{exchange} publish", ActivityKind.Producer), exchange, messageId, eventType);

    public static Activity? StartConsumerActivity(IDictionary<string, object?>? headers,
        string queue, string? messageId, string? eventType)
    {
        var parent = Propagator.Extract(default, headers, ReadHeader).ActivityContext;
        // A default ActivityContext would fall back to Activity.Current. An explicit empty
        // parent ID instead starts a root for messages with absent or malformed context.
        var activity = parent != default
            ? Source.StartActivity($"{queue} process", ActivityKind.Consumer, parent)
            : Source.CreateActivity($"{queue} process", ActivityKind.Consumer,
                parentId: string.Empty, idFormat: ActivityIdFormat.W3C)?.Start();
        Tag(activity, queue, messageId, eventType);
        activity?.SetTag("correlation.id", ReadCorrelationId(headers));
        return activity;
    }

    private static Activity? Tag(Activity? activity, string destination, string? messageId, string? eventType)
    {
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", destination);
        activity?.SetTag("messaging.message.id", messageId);
        activity?.SetTag("messaging.message.type", eventType);
        return activity;
    }

    private static IEnumerable<string> ReadHeader(IDictionary<string, object?>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out var value))
            return [];
        return value switch
        {
            string text => [text],
            byte[] bytes => [Encoding.UTF8.GetString(bytes)],
            _ => []
        };
    }
}
