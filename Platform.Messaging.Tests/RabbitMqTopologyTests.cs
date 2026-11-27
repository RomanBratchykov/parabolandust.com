using RabbitMQ.Client;
using Xunit;

namespace Platform.Messaging.Tests;

public sealed class RabbitMqTopologyTests
{
    [Fact]
    public async Task Durable_subscriptions_have_independent_dead_letter_routes_and_expected_bindings()
    {
        var broker = new BrokerDouble();
        using var cancellation = new CancellationTokenSource();
        await RabbitMqTopology.DeclareAsync(broker.Channel, cancellation.Token);
        var exchanges = broker.Calls.Where(call => call.Method == "ExchangeDeclareAsync").ToArray();
        Assert.Equal(2, exchanges.Length);
        Assert.Contains(exchanges, call => Equals(call.Arguments["exchange"], "account-events") && Equals(call.Arguments["type"], ExchangeType.Topic));
        Assert.Contains(exchanges, call => Equals(call.Arguments["exchange"], "account-events.dlx") && Equals(call.Arguments["type"], ExchangeType.Direct));
        foreach (var call in exchanges)
        {
            Assert.Equal(true, call.Arguments["durable"]);
            Assert.Equal(false, call.Arguments["autoDelete"]);
            Assert.Equal(false, call.Arguments["noWait"]);
        }
        var queues = broker.Calls.Where(call => call.Method == "QueueDeclareAsync").ToArray();
        Assert.Equal(4, queues.Length);
        foreach (var call in queues)
        {
            Assert.Equal(true, call.Arguments["durable"]);
            Assert.Equal(false, call.Arguments["exclusive"]);
            Assert.Equal(false, call.Arguments["autoDelete"]);
        }
        foreach (var queue in new[] { "newsletters.account-sync", "gamestats.account-sync" })
        {
            var main = Assert.Single(queues, call => Equals(call.Arguments["queue"], queue));
            var arguments = Assert.IsType<Dictionary<string, object?>>(main.Arguments["arguments"]);
            Assert.Equal("account-events.dlx", arguments["x-dead-letter-exchange"]);
            Assert.Equal(queue, arguments["x-dead-letter-routing-key"]);
            Assert.Contains(broker.Calls, call => call.Method == "QueueBindAsync" &&
                Equals(call.Arguments["queue"], queue + ".dlq") && Equals(call.Arguments["exchange"], "account-events.dlx") &&
                Equals(call.Arguments["routingKey"], queue));
            Assert.Contains(broker.Calls, call => call.Method == "QueueBindAsync" &&
                Equals(call.Arguments["queue"], queue) && Equals(call.Arguments["exchange"], "account-events") &&
                Equals(call.Arguments["routingKey"], "account.*"));
        }
        Assert.Contains(broker.Calls, call => call.Method == "QueueBindAsync" &&
            Equals(call.Arguments["queue"], "gamestats.account-sync") && Equals(call.Arguments["routingKey"], "achievement.*"));
        Assert.DoesNotContain(broker.Calls, call => call.Method == "QueueBindAsync" &&
            Equals(call.Arguments["queue"], "newsletters.account-sync") && Equals(call.Arguments["routingKey"], "achievement.*"));
        Assert.All(broker.Calls, call => Assert.Equal(cancellation.Token, call.Arguments["cancellationToken"]));
    }
}
