using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Platform.ServiceDefaults;

public sealed class TraceLogEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "ThreadId", Environment.CurrentManagedThreadId));

        if (Activity.Current is { } activity)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "TraceId", activity.TraceId.ToHexString()));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "SpanId", activity.SpanId.ToHexString()));
        }
    }
}
