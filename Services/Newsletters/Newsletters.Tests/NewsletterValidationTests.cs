using Newsletters.Bll.Contracts;
using Newsletters.Bll.Validation;
using Xunit;

namespace Newsletters.Tests;

public sealed class NewsletterValidationTests
{
    [Fact]
    public void Create_request_reports_empty_title_and_main_info()
    {
        var result = new CreateNewsletterRequestValidator()
            .Validate(new CreateNewsletterRequest(4001, "", "", null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateNewsletterRequest.Title));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateNewsletterRequest.MainInfo));
    }

    [Fact]
    public void Query_rejects_unapproved_sort_field_and_overflowing_offset()
    {
        var query = new NewsletterQuery(Page: int.MaxValue, PageSize: 100, SortBy: "title; DROP TABLE");

        var result = new NewsletterQueryValidator().Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(NewsletterQuery.SortBy));
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("offset", StringComparison.OrdinalIgnoreCase));
    }
}
