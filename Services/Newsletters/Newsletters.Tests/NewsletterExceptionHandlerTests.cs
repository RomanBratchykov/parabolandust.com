using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Newsletters.Api.ExceptionHandling;
using Xunit;

namespace Newsletters.Tests;

public sealed class NewsletterExceptionHandlerTests
{
    [Fact]
    public async Task Concurrent_update_returns_409_problem_details_with_the_request_trace()
    {
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "concurrency-test" };
        context.Request.Path = "/api/newsletters/1/publish";
        using var body = new MemoryStream();
        context.Response.Body = body;
        var handler = new NewsletterExceptionHandler(NullLogger<NewsletterExceptionHandler>.Instance,
            new HostingEnvironment { EnvironmentName = Environments.Production });

        var handled = await handler.TryHandleAsync(context, new DbUpdateConcurrencyException(), default);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(body);
        Assert.Equal(409, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("concurrency-test", problem.RootElement.GetProperty("traceId").GetString());
    }
}
