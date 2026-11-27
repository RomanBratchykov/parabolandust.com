using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Platform.ServiceDefaults;

public sealed class BusinessMetrics
{
    private readonly Counter<long> _completed;
    private readonly Histogram<double> _duration;
    private long _active;

    public BusinessMetrics(IMeterFactory meterFactory, string service, string prefix)
    {
        var meter = meterFactory.Create(service);
        _completed = meter.CreateCounter<long>($"{prefix}.operations.completed", "{operation}", "Successful business operations");
        _duration = meter.CreateHistogram<double>($"{prefix}.operation.duration", "ms", "Business operation duration, including failures");
        meter.CreateObservableGauge($"{prefix}.operations.active", () => Interlocked.Read(ref _active),
            "{operation}", "Business operations currently executing in this process");
    }

    public Operation Start(string operation)
    {
        Interlocked.Increment(ref _active);
        return new Operation(this, operation);
    }

    public sealed class Operation(BusinessMetrics metrics, string name) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        private bool _success;
        private int _disposed;

        public void Succeed() => _success = true;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Interlocked.Decrement(ref metrics._active);
            var tags = new TagList { { "operation", name }, { "status", _success ? "success" : "failure" } };
            if (_success) metrics._completed.Add(1, tags);
            metrics._duration.Record(Stopwatch.GetElapsedTime(_started).TotalMilliseconds, tags);
        }
    }
}
