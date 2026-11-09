using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http.Resilience;
using Polly.Timeout;

namespace Platform.Aggregator.Clients;

public static class ClientRegistration
{
    public static IServiceCollection AddDashboardClients(this IServiceCollection services)
    {
        Register<AccountsClient>(services, "accounts-api");
        Register<NewslettersClient>(services, "newsletters-api");
        Register<GameStatsClient>(services, "gamestats-api");
        return services;
    }

    private static void Register<TClient>(IServiceCollection services, string service) where TClient : class
    {
#pragma warning disable EXTEXP0001 // Required to replace the shared resilience pipeline's timeout budget.
        services.AddHttpClient<TClient>(http =>
        {
            http.BaseAddress = new Uri($"https+http://{service}");
            http.Timeout = Timeout.InfiniteTimeSpan;
        })
        // Keep shared discovery/correlation, but replace the default resilience budget.
        .RemoveAllResilienceHandlers()
        .AddStandardResilienceHandler()
        .Configure((options, provider) =>
        {
            var seconds = provider.GetRequiredService<IOptions<DownstreamOptions>>().Value.TimeoutSeconds;
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(seconds);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(seconds);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        });
#pragma warning restore EXTEXP0001
    }
}

internal static class DownstreamJson
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public static async Task<T?> GetAsync<T>(HttpClient http, DownstreamOptions options,
        ILogger logger, string service, string path, CancellationToken cancellationToken) where T : class
    {
        var started = Stopwatch.GetTimestamp();
        int? statusCode = null;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        logger.LogInformation("Calling {Service} at {Path}", service, path);
        try
        {
            using var response = await http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, budget.Token);
            statusCode = (int)response.StatusCode;
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(Json, budget.Token)
                ?? throw new JsonException($"{service} returned a null payload.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{service} exceeded its downstream budget.", exception);
        }
        catch (TimeoutRejectedException exception)
        {
            throw new TimeoutException($"{service} exceeded its downstream budget.", exception);
        }
        finally
        {
            logger.LogInformation("{Service} call finished with {StatusCode} in {ElapsedMilliseconds} ms",
                service, statusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
