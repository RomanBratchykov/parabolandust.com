using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newsletters.Bll.Contracts;
using Xunit;

namespace Newsletters.Tests;

public sealed class NewsletterMvcValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Valid_request_records_pass_MVC_validation(int requestType)
    {
        object request = requestType switch
        {
            0 => new CreateNewsletterRequest(4001, "Update", "Release notes", null),
            1 => new PublishNewsletterRequest("editor_demo"),
            2 => new CreateSubscriptionRequest(1001, 1),
            _ => new NewsletterQuery()
        };

        Assert.True(Validate(request).IsValid);
    }

    [Fact]
    public void Empty_newsletter_fields_produce_field_errors_in_MVC()
    {
        var state = Validate(new CreateNewsletterRequest(4001, "", "", null));

        Assert.False(state.IsValid);
        Assert.Contains(nameof(CreateNewsletterRequest.Title), state.Keys);
        Assert.Contains(nameof(CreateNewsletterRequest.MainInfo), state.Keys);
    }

    [Fact]
    public void Invalid_pagination_produces_field_errors_in_MVC()
    {
        var state = Validate(new NewsletterQuery(Page: 0, PageSize: 101));

        Assert.False(state.IsValid);
        Assert.Contains(nameof(NewsletterQuery.Page), state.Keys);
        Assert.Contains(nameof(NewsletterQuery.PageSize), state.Keys);
    }

    private static ModelStateDictionary Validate(object request)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var action = new ActionContext(new DefaultHttpContext { RequestServices = provider },
            new RouteData(), new ActionDescriptor());
        provider.GetRequiredService<IObjectModelValidator>().Validate(action, null, "", request);
        return action.ModelState;
    }
}
