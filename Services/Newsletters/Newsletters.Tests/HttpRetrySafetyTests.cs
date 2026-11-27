using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.ServiceDefaults;
using Xunit;

namespace Newsletters.Tests;

public sealed class HttpRetrySafetyTests
{
    [Theory]
    [InlineData("GET", 4)]
    [InlineData("POST", 1)]
    [InlineData("PUT", 1)]
    [InlineData("PATCH", 1)]
    [InlineData("DELETE", 1)]
    [InlineData("CONNECT", 1)]
    public async Task Shared_defaults_retry_reads_without_replaying_writes(string method, int expectedAttempts)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        var transport = new UnavailableTransport();
        builder.Services.AddHttpClient("retry-safety")
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        using var host = builder.Build();
        using var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("retry-safety");
        using var request = new HttpRequestMessage(new HttpMethod(method), "https://localhost/retry-safety");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(expectedAttempts, transport.Attempts);
    }

    private sealed class UnavailableTransport : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
