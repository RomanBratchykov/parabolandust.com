using Accounts.Grpc;
using GameStats.Grpc;
using Microsoft.Extensions.Http.Resilience;
using Newsletters.Grpc;

namespace Platform.Aggregator.Clients;

public static class GrpcClientRegistration
{
    public static IServiceCollection AddGrpcDashboardClients(this IServiceCollection services)
    {
        Register<AccountsReader.AccountsReaderClient>(services, "accounts-api");
        Register<NewslettersReader.NewslettersReaderClient>(services, "newsletters-api");
        Register<GameStatsReader.GameStatsReaderClient>(services, "gamestats-api");
        services.AddTransient<GrpcCall>();
        return services;
    }

    private static void Register<TClient>(IServiceCollection services, string service) where TClient : class
    {
#pragma warning disable EXTEXP0001 // RPC deadlines replace the shared HTTP resilience budget.
        services.AddGrpcClient<TClient>(options => options.Address = new Uri($"https://{service}"))
            .ConfigureHttpClient(http => http.Timeout = Timeout.InfiniteTimeSpan)
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        // Shared HttpClient defaults retain discovery and request correlation. No RPC retries.
    }
}
