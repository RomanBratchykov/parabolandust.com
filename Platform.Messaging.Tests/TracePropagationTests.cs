using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using Platform.Messaging.Diagnostics;
using Xunit;

namespace Platform.Messaging.Tests;

public sealed class TracePropagationTests : IDisposable
{
    private readonly ActivityListener listener = new()
    {
        ShouldListenTo = source => source.Name == "Platform.Messaging",
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded
    };

    public TracePropagationTests() => ActivitySource.AddActivityListener(listener);
    public void Dispose() => listener.Dispose();

    [Fact]
    public void Producer_context_round_trips_through_broker_byte_headers()
    {
        using var request = new Activity("request").Start();
        request.TraceStateString = "vendor=value";
        using var producer = TracePropagation.StartProducerActivity("account-events", "AccountCreatedEvent", "event-1");
        var headers = new Dictionary<string, object?>();
        TracePropagation.Inject(headers, "request-123");
        Assert.Equal(ActivityKind.Producer, producer!.Kind);
        Assert.Equal(request.TraceId, producer.TraceId);
        Assert.Equal("rabbitmq", producer.GetTagItem("messaging.system"));
        var incoming = headers.ToDictionary(pair => pair.Key,
            pair => (object?)Encoding.UTF8.GetBytes(Assert.IsType<string>(pair.Value)));

        using var consumer = TracePropagation.StartConsumerActivity(incoming, "newsletters.accounts", "event-1", "AccountCreatedEvent");
        Assert.Equal(producer.TraceId, consumer!.TraceId);
        Assert.Equal(producer.SpanId, consumer.ParentSpanId);
        Assert.Equal("vendor=value", consumer.TraceStateString);
        Assert.Equal(ActivityKind.Consumer, consumer.Kind);
        Assert.Equal("newsletters.accounts", consumer.GetTagItem("messaging.destination.name"));
        Assert.Equal("event-1", consumer.GetTagItem("messaging.message.id"));
        Assert.Equal("AccountCreatedEvent", consumer.GetTagItem("messaging.message.type"));
        Assert.Equal("request-123", consumer.GetTagItem("correlation.id"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-parent")]
    [InlineData("00-00000000000000000000000000000000-0000000000000000-01")]
    public void Missing_or_invalid_parent_starts_a_root_and_restores_ambient_activity(string? parent)
    {
        using var unrelated = new Activity("unrelated").Start();
        var headers = new Dictionary<string, object?> { ["traceparent"] = parent };
        using (var consumer = TracePropagation.StartConsumerActivity(headers, "queue", "id", "type"))
        {
            Assert.NotEqual(unrelated.TraceId, consumer!.TraceId);
            Assert.Equal(default, consumer.ParentSpanId);
        }
        Assert.Same(unrelated, Activity.Current);
    }

    [Fact]
    public void String_headers_are_supported_and_incoming_baggage_does_not_change_ambient_state()
    {
        var previous = Baggage.Current;
        try
        {
            Baggage.Current = Baggage.SetBaggage("local", "private");
            using var request = new Activity("request").Start();
            var headers = new Dictionary<string, object?> { ["baggage"] = "remote=secret" };
            TracePropagation.Inject(headers, "correlation");
            Assert.False(headers.ContainsKey("baggage"));
            headers["baggage"] = Encoding.UTF8.GetBytes("remote=secret");
            using var consumer = TracePropagation.StartConsumerActivity(headers, "queue", "id", "type");
            Assert.Equal(request.TraceId, consumer!.TraceId);
            Assert.Equal("correlation", TracePropagation.ReadCorrelationId(headers));
            Assert.Equal("private", Baggage.GetBaggage("local"));
            Assert.Null(Baggage.GetBaggage("remote"));
        }
        finally { Baggage.Current = previous; }
    }

    [Fact]
    public void No_activity_clears_stale_propagation_headers_without_removing_business_headers()
    {
        var current = Activity.Current;
        try
        {
            Activity.Current = null;
            var headers = new Dictionary<string, object?>
            {
                ["traceparent"] = "stale", ["tracestate"] = "stale", ["baggage"] = "stale",
                ["x-correlation-id"] = "stale", ["x-retry-count"] = 2
            };
            TracePropagation.Inject(headers, null);
            Assert.Single(headers);
            Assert.Equal(2, headers["x-retry-count"]);
            Assert.Null(TracePropagation.ReadCorrelationId(null));
            using var consumer = TracePropagation.StartConsumerActivity(null, "queue", "id", "type");
            Assert.Equal(default, consumer!.ParentSpanId);
        }
        finally { Activity.Current = current; }
    }
}
