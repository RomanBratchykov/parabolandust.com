using System.Net;
using System.Net.Http.Headers;
using Accounts.Grpc;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Aggregator.Clients;
using Platform.ServiceDefaults;
using Xunit;

namespace Platform.Aggregator.Tests;

public sealed class GrpcRetryTests
{
    [Theory]
    [InlineData("accounts", StatusCode.Unavailable, 4)]
    [InlineData("accounts", StatusCode.ResourceExhausted, 4)]
    [InlineData("accounts", StatusCode.InvalidArgument, 1)]
    [InlineData("accounts", StatusCode.NotFound, 1)]
    [InlineData("newsletters", StatusCode.Unavailable, 2)]
    [InlineData("newsletters", StatusCode.ResourceExhausted, 2)]
    [InlineData("newsletters", StatusCode.InvalidArgument, 1)]
    [InlineData("newsletters", StatusCode.NotFound, 1)]
    [InlineData("gamestats", StatusCode.Unavailable, 2)]
    [InlineData("gamestats", StatusCode.ResourceExhausted, 2)]
    [InlineData("gamestats", StatusCode.InvalidArgument, 1)]
    [InlineData("gamestats", StatusCode.NotFound, 1)]
    public async Task Reads_retry_only_transient_grpc_statuses(string service, StatusCode status, int attempts)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        builder.Services.AddGrpcDashboardClients();
        var transport = new GrpcFailureTransport(status);
        builder.Services.AddGrpcClient<AccountsReader.AccountsReaderClient>()
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        builder.Services.AddGrpcClient<Newsletters.Grpc.NewslettersReader.NewslettersReaderClient>()
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        builder.Services.AddGrpcClient<GameStats.Grpc.GameStatsReader.GameStatsReaderClient>()
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        using var host = builder.Build();
        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            switch (service)
            {
                case "accounts":
                    await host.Services.GetRequiredService<AccountsReader.AccountsReaderClient>()
                        .GetUserAsync(new UserRequest { UserId = 1 }, deadline: deadline);
                    break;
                case "newsletters":
                    await host.Services.GetRequiredService<Newsletters.Grpc.NewslettersReader.NewslettersReaderClient>()
                        .GetUserSubscriptionsAsync(new Newsletters.Grpc.UserRequest { UserId = 1 }, deadline: deadline);
                    break;
                default:
                    await host.Services.GetRequiredService<GameStats.Grpc.GameStatsReader.GameStatsReaderClient>()
                        .GetStatisticsAsync(new GameStats.Grpc.UserRequest { UserId = 1 }, deadline: deadline);
                    break;
            }
        });

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(attempts, transport.Attempts);
    }

    private sealed class GrpcFailureTransport(StatusCode status) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Attempts++;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Version = HttpVersion.Version20,
                Content = new ByteArrayContent([])
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            response.Headers.Add("grpc-status", ((int)status).ToString());
            return Task.FromResult(response);
        }
    }
}
