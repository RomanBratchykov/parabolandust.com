using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Platform.ServiceDefaults;

public static class LoggingExtensions
{
    public static TBuilder AddDefaultLogging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Only the OTEL provider is added back; Serilog owns console output.
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((services, logging) => logging
            .MinimumLevel.Is(builder.Environment.IsDevelopment()
                ? LogEventLevel.Debug : LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ServiceName", builder.Configuration["OTEL_SERVICE_NAME"]
                ?? builder.Environment.ApplicationName)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.With(new TraceLogEnricher())
            .WriteTo.Console(new CompactJsonFormatter()), writeToProviders: true);

        return builder;
    }
}
