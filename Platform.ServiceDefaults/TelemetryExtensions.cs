using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Platform.Caching;

namespace Platform.ServiceDefaults;

public static class TelemetryExtensions
{
    public static TBuilder AddDefaultTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"]
            ?? builder.Environment.ApplicationName;
        var sampler = CreateSampler(builder);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment.name"] = builder.Environment.EnvironmentName
                }))
            .WithMetrics(metrics => metrics
                .AddMeter(CacheMetrics.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .SetSampler(sampler)
                .AddSource(CacheMetrics.MeterName, "Platform.Grpc")
                .AddSource(builder.Environment.ApplicationName, serviceName,
                    "Platform.*", "Npgsql", "MongoDB.Driver.Core.Extensions.DiagnosticSources")
                .AddAspNetCoreInstrumentation(options => options.Filter = context =>
                    !context.Request.Path.StartsWithSegments("/health") &&
                    !context.Request.Path.StartsWithSegments("/alive"))
                .AddHttpClientInstrumentation());

        // Standalone services do not require a collector to be running.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }

    private static Sampler CreateSampler(IHostApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            return new AlwaysOnSampler();
        }

        var configured = builder.Configuration["OpenTelemetry:SamplingRatio"] ?? "0.1";
        if (!double.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var ratio) || !double.IsFinite(ratio) || ratio is < 0 or > 1)
        {
            throw new InvalidOperationException("OpenTelemetry:SamplingRatio must be between 0 and 1.");
        }

        return new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio));
    }
}
