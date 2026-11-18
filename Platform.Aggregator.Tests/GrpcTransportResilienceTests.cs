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

public sealed class GrpcTransportResilienceTests
{
    [Fact]
    public async Task Persistent_transport_failures_open_the_circuit_without_sending_more_requests()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        builder.Services.AddGrpcDashboardClients();
        var transport = new UnavailableTransport();
        builder.Services.AddGrpcClient<AccountsReader.AccountsReaderClient>()
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        using var host = builder.Build();
        var client = host.Services.GetRequiredService<AccountsReader.AccountsReaderClient>();

        await Assert.ThrowsAsync<RpcException>(async () =>
            await client.GetUserAsync(new UserRequest { UserId = 1 }, deadline: DateTime.UtcNow.AddSeconds(5)));
        Assert.Equal(4, transport.Attempts);

        await Assert.ThrowsAsync<RpcException>(async () =>
            await client.GetUserAsync(new UserRequest { UserId = 1 }, deadline: DateTime.UtcNow.AddSeconds(5)));
        Assert.Equal(4, transport.Attempts);
    }

    private sealed class UnavailableTransport : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Attempts++;
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Version = HttpVersion.Version20,
                Content = new ByteArrayContent([])
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            return Task.FromResult(response);
        }
    }
}
