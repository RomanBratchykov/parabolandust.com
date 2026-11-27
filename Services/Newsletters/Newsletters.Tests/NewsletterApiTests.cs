using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Newsletters.Tests;

public sealed class ApiFactAttribute : FactAttribute
{
    public ApiFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEWSLETTERS_TEST_URL")))
            Skip = "Set NEWSLETTERS_TEST_URL to the API running against a disposable test database.";
    }
}

[Collection("Lab 3 database")]
public sealed class NewsletterApiTests : IDisposable
{
    private readonly HttpClient client = new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("NEWSLETTERS_TEST_URL") ?? "http://localhost")
    };

    [ApiFact]
    public async Task Create_get_and_publish_return_201_200_204_and_409()
    {
        using var created = await client.PostAsJsonAsync("/api/newsletters",
            new { adminId = 4001, title = "HTTP lifecycle test", mainInfo = "Release notes" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var newsletter = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = newsletter.GetProperty("id").GetInt64();
        using var fetched = await client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(id, (await fetched.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64());

        using var published = await client.PostAsJsonAsync($"/api/newsletters/{id}/publish", new { publishedBy = "test" });
        Assert.Equal(HttpStatusCode.NoContent, published.StatusCode);
        using var repeated = await client.PostAsJsonAsync($"/api/newsletters/{id}/publish", new { publishedBy = "test" });
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        Assert.Equal("application/problem+json", repeated.Content.Headers.ContentType?.MediaType);
    }

    [ApiFact]
    public async Task Empty_newsletter_returns_400_with_field_errors()
    {
        using var response = await client.PostAsJsonAsync("/api/newsletters",
            new { adminId = 4001, title = "", mainInfo = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("Title", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("MainInfo", out _));
    }

    [ApiFact]
    public async Task Invalid_sort_returns_400_with_validation_problem_details()
    {
        using var response = await client.GetAsync("/api/newsletters?sortBy=invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(problem.GetProperty("errors").TryGetProperty("SortBy", out _));
    }

    [ApiFact]
    public async Task Missing_newsletter_returns_404_problem_details()
    {
        using var response = await client.GetAsync("/api/newsletters/9223372036854775807");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [ApiFact]
    public async Task Duplicate_subscription_returns_409()
    {
        var page = await client.GetFromJsonAsync<JsonElement>("/api/newsletters?search=Development%20Update");
        var newsletterId = page.GetProperty("items")[0].GetProperty("id").GetInt64();
        using var response = await client.PostAsJsonAsync("/api/subscriptions", new { userId = 1001, newsletterId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [ApiFact]
    public async Task Second_page_returns_correct_items_and_metadata()
    {
        var prefix = $"HTTP paging {Guid.NewGuid():N}";
        for (var index = 1; index <= 12; index++)
        {
            using var created = await client.PostAsJsonAsync("/api/newsletters",
                new { adminId = 4001, title = $"{prefix} {index:00}", mainInfo = "Release notes" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var page = await client.GetFromJsonAsync<JsonElement>(
            $"/api/newsletters?page=2&pageSize=5&search={Uri.EscapeDataString(prefix)}&sortBy=title&sortDir=asc");

        Assert.Equal(12, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.Equal(5, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, page.GetProperty("totalPages").GetInt32());
        Assert.True(page.GetProperty("hasNext").GetBoolean());
        Assert.True(page.GetProperty("hasPrevious").GetBoolean());
        Assert.Equal(new[] { $"{prefix} 06", $"{prefix} 07", $"{prefix} 08", $"{prefix} 09", $"{prefix} 10" },
            page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("title").GetString()));
    }

    [ApiFact]
    public async Task Swagger_describes_newsletter_endpoints()
    {
        var document = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");

        Assert.True(document.GetProperty("paths").TryGetProperty("/api/newsletters", out _));
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/newsletters/{id}/publish", out _));
    }

    public void Dispose() => client.Dispose();
}
