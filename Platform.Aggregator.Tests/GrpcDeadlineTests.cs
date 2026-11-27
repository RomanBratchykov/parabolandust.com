using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Platform.Aggregator.Clients;
using Xunit;

namespace Platform.Aggregator.Tests;

public sealed class GrpcDeadlineTests
{
    [Theory]
    [InlineData("accounts-api", 5, 5)]
    [InlineData("newsletters-api", 5, 2)]
    [InlineData("gamestats-api", 5, 2)]
    [InlineData("newsletters-api", 1, 1)]
    public async Task Optional_reads_use_a_shorter_deadline_within_the_configured_budget(
        string service, int configuredSeconds, int expectedSeconds)
    {
        var calls = new GrpcCall(Options.Create(new DownstreamOptions { TimeoutSeconds = configuredSeconds }),
            NullLogger<GrpcCall>.Instance);
        var before = DateTime.UtcNow;
        DateTime? deadline = null;

        await calls.InvokeAsync(service, "test/Read", (value, _) =>
        {
            deadline = value;
            return new AsyncUnaryCall<object>(Task.FromResult(new object()), Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }, CancellationToken.None);

        Assert.InRange(deadline!.Value, before.AddSeconds(expectedSeconds), DateTime.UtcNow.AddSeconds(expectedSeconds));
    }
}
