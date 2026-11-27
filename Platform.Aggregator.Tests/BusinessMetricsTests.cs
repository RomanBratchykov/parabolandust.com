using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.ServiceDefaults;
using Xunit;

namespace Platform.Aggregator.Tests;

public sealed class BusinessMetricsTests
{
    [Fact]
    public void Operations_count_only_successes_and_record_all_durations_and_active_work()
    {
        var completed = new List<long>();
        var durations = new List<string>();
        var active = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "Accounts.Api") meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            if (instrument.Name == "accounts.operations.completed") completed.Add(value);
            if (instrument.Name == "accounts.operations.active") active.Add(value);
        });
        listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
        {
            Assert.True(value >= 0);
            Assert.Equal(2, tags.Length);
            Assert.Equal("operation", tags[0].Key);
            Assert.Equal("create_user", tags[0].Value);
            Assert.Equal("status", tags[1].Key);
            durations.Add((string)tags[1].Value!);
        });
        listener.Start();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ApplicationName = "Accounts.Api" });
        builder.AddServiceDefaults();
        using var host = builder.Build();

        // Resolve through DI so the service's singleton registration is part of this check.
        var business = host.Services.GetRequiredService<BusinessMetrics>();
        using (var successful = business.Start("create_user"))
        {
            listener.RecordObservableInstruments();
            successful.Succeed();
        }
        var failed = business.Start("create_user");
        failed.Dispose();
        failed.Dispose();
        listener.RecordObservableInstruments();

        Assert.Equal(new long[] { 1 }, completed);
        Assert.Equal(new[] { "success", "failure" }, durations);
        Assert.Equal(new long[] { 1, 0 }, active);
    }
}
