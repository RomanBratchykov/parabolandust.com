using Microsoft.EntityFrameworkCore;
using Newsletters.Bll.Contracts;
using Newsletters.Bll.Specifications;
using Newsletters.Dal.Data;
using Newsletters.Dal.Repositories;
using Xunit;

namespace Newsletters.Tests;

public sealed class NewsletterQueryTests
{
    [Theory]
    [InlineData("title", "title")]
    [InlineData("publishedAt", "published_at")]
    [InlineData("createdAt", "created_at")]
    public void Paging_uses_the_newsletter_id_to_break_equal_sort_values(string sortBy, string column)
    {
        var options = new DbContextOptionsBuilder<NewslettersDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        using var context = new NewslettersDbContext(options);
        var specification = new NewslettersFilterSpecification(
            new NewsletterQuery(SortBy: sortBy, SortDir: "asc"), pageSize: 5);

        var sql = SpecificationEvaluator.Apply(context.Newsletters, specification).ToQueryString();

        Assert.Matches($@"ORDER BY \w+\.{column}, \w+\.newsletter_id", sql);
    }

    [Fact]
    public void Filter_specification_keeps_search_sort_and_paging_in_sql()
    {
        var options = new DbContextOptionsBuilder<NewslettersDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        using var context = new NewslettersDbContext(options);
        var query = new NewsletterQuery(Page: 2, PageSize: 5, Search: "update", SortBy: "publishedAt", SortDir: "desc");
        var specification = new NewslettersFilterSpecification(query, pageSize: 5);

        var sql = SpecificationEvaluator.Apply(context.Newsletters, specification).ToQueryString();

        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("DESC", sql);
        Assert.Contains("LIKE", sql);
    }
}
