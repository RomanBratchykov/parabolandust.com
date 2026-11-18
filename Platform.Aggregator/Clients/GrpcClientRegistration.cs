using Accounts.Grpc;
using GameStats.Grpc;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Newsletters.Grpc;

namespace Platform.Aggregator.Clients;

public static class GrpcClientRegistration
{
    public static IServiceCollection AddGrpcDashboardClients(this IServiceCollection services)
    {
        // Accounts is required; optional projections get fewer attempts for faster partial responses.
        Register<AccountsReader.AccountsReaderClient>(services, "accounts-api", "accounts.v1.AccountsReader", "GetUser", 4);
        Register<NewslettersReader.NewslettersReaderClient>(services, "newsletters-api", "newsletters.v1.NewslettersReader", "GetUserSubscriptions", 2);
        Register<GameStatsReader.GameStatsReaderClient>(services, "gamestats-api", "gamestats.v1.GameStatsReader", "GetStatistics", 2);
        services.AddTransient<GrpcCall>();
        return services;
    }

    private static void Register<TClient>(IServiceCollection services, string service,
        string grpcService, string method, int maxAttempts) where TClient : class
    {
#pragma warning disable EXTEXP0001 // Replace shared defaults rather than stacking resilience handlers.
        services.AddGrpcClient<Health.HealthClient>(service, options => options.Address = new Uri($"https://{service}"))
            .ConfigureHttpClient(http => http.Timeout = Timeout.InfiniteTimeSpan)
            .RemoveAllResilienceHandlers();
        services.AddHealthChecks().Add(new HealthCheckRegistration(service, provider =>
            new GrpcDependencyHealthCheck(provider.GetRequiredService<GrpcClientFactory>()
                .CreateClient<Health.HealthClient>(service), service,
                provider.GetRequiredService<ILogger<GrpcDependencyHealthCheck>>()),
            HealthStatus.Unhealthy, ["grpc", "downstream", "ready"], TimeSpan.FromSeconds(3)));
        services.AddGrpcClient<TClient>(options => options.Address = new Uri($"https://{service}"))
            .ConfigureHttpClient(http => http.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigureChannel(channel => channel.ServiceConfig = new ServiceConfig
            {
                MethodConfigs =
                {
                    new MethodConfig
                    {
                        Names = { new MethodName { Service = grpcService, Method = method } },
                        RetryPolicy = new RetryPolicy
                        {
                            MaxAttempts = maxAttempts,
                            InitialBackoff = TimeSpan.FromSeconds(0.2),
                            MaxBackoff = TimeSpan.FromSeconds(1),
                            BackoffMultiplier = 2,
                            RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.ResourceExhausted }
                        }
                    }
                }
            })
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(options =>
            {
                var required = service == "accounts-api";
                // gRPC owns retries of read-only POST calls; HTTP retries would multiply attempts.
                options.Retry.DisableForUnsafeHttpMethods();
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(required ? 2 : 1);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(required ? 5 : 2);
                options.CircuitBreaker.FailureRatio = required ? 0.3 : 0.7;
                options.CircuitBreaker.MinimumThroughput = required ? 4 : 6;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(required ? 10 : 5);
            });
#pragma warning restore EXTEXP0001
        // Only named read RPCs retry; future write methods do not inherit a retry policy.
    }
}
