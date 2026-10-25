using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Newsletters.Tests;

public sealed class LogFactAttribute : FactAttribute
{
    public LogFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEWSLETTERS_TEST_URL"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEWSLETTERS_TEST_LOG")))
            Skip = "Set NEWSLETTERS_TEST_URL and NEWSLETTERS_TEST_LOG to the test API and its console log.";
    }
}

[Collection("Lab 3 database")]
public sealed class NewsletterLogTests
{
    [LogFact]
    public async Task Exception_is_logged_once_with_correlation_user_and_request_properties()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("NEWSLETTERS_TEST_URL")!)
        };
        using var response = await client.GetAsync("/api/newsletters/9223372036854775807");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var traceId = problem.GetProperty("traceId").GetString();
        var events = File.ReadLines(Environment.GetEnvironmentVariable("NEWSLETTERS_TEST_LOG")!)
            .Where(line => line.StartsWith('{'))
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line))
            .Where(log => log.TryGetProperty("Properties", out var properties)
                && properties.TryGetProperty("CorrelationId", out var correlation)
                && correlation.GetString() == traceId)
            .ToList();

        var error = Assert.Single(events.Where(log => log.TryGetProperty("Exception", out _)));
        Assert.Equal("anonymous", error.GetProperty("Properties").GetProperty("UserId").GetString());
        Assert.Equal("/api/newsletters/9223372036854775807",
            error.GetProperty("Properties").GetProperty("RequestPath").GetString());
        Assert.Contains(events, log => log.GetProperty("Properties").TryGetProperty("StatusCode", out var status)
            && status.GetInt32() == 404);
    }
}
