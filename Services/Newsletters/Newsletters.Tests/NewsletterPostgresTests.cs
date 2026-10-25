using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Newsletters.Bll.Contracts;
using Newsletters.Bll.Specifications;
using Newsletters.Dal.Data;
using Newsletters.Dal.Repositories;
using Newsletters.Dal.Seeding;
using Newsletters.Domain.Models;
using Xunit;

namespace Newsletters.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEWSLETTERS_TEST_CONNECTION")))
            Skip = "Set NEWSLETTERS_TEST_CONNECTION to an empty, disposable PostgreSQL database.";
    }
}

[Collection("Lab 3 database")]
public sealed class NewsletterPostgresTests
{
    [PostgresFact]
    public async Task Migrations_and_repeated_seeding_preserve_one_sample_graph()
    {
        await using var context = await OpenContextAsync();
        var counts = (await context.Newsletters.CountAsync(), await context.Subscriptions.CountAsync(),
            await context.DeliverySettings.CountAsync());

        await DbInitializer.SeedAsync(context);

        Assert.Equal(counts, (await context.Newsletters.CountAsync(), await context.Subscriptions.CountAsync(),
            await context.DeliverySettings.CountAsync()));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Eager_loading_includes_comment_authors_in_one_query()
    {
        var commands = new QueryCounter();
        await using var context = await OpenContextAsync(commands);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var newsletterId = await context.Newsletters.Select(value => value.Id).FirstAsync();
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO newsletter.users_copy (user_id, user_name, source_updated_at)
            VALUES (2002, 'comment_author', CURRENT_TIMESTAMP)
            """);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO newsletter.user_comments (user_id, newsletter_id, comment_text)
            VALUES (2002, {newsletterId}, 'A comment from a non-subscriber')
            """);
        context.ChangeTracker.Clear();
        commands.Count = 0;

        var newsletter = await new NewsletterRepository(context).GetWithDetailsAsync(newsletterId);

        Assert.Equal(1, commands.Count);
        Assert.Equal("comment_author", Assert.Single(newsletter!.Comments).User?.UserName);
    }

    [PostgresFact]
    public async Task Explicit_loading_returns_subscriptions_in_two_queries()
    {
        var commands = new QueryCounter();
        await using var context = await OpenContextAsync(commands);
        context.ChangeTracker.Clear();
        commands.Count = 0;

        var user = await new UserCopyRepository(context).GetWithSubscriptionsAsync(1001);

        Assert.Equal(2, commands.Count);
        Assert.Equal("Development Update", Assert.Single(user!.Subscriptions).Newsletter.Title);
    }

    [PostgresFact]
    public async Task Many_to_many_query_filters_by_subscription_date()
    {
        await using var context = await OpenContextAsync();
        var newsletterId = await context.Newsletters.Select(value => value.Id).FirstAsync();
        var repository = new SubscriptionRepository(context);

        var subscribers = await repository.GetActiveSubscribersAsync(newsletterId, DateTimeOffset.MinValue);
        var futureSubscribers = await repository.GetActiveSubscribersAsync(newsletterId, DateTimeOffset.UtcNow.AddDays(1));

        Assert.Equal(1001, Assert.Single(subscribers).Id);
        Assert.Empty(futureSubscribers);
    }

    [PostgresFact]
    public async Task Filtered_second_page_preserves_total_count_and_title_order()
    {
        await using var context = await OpenContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Newsletters.AddRange(Enumerable.Range(1, 12)
            .Select(index => new Newsletter(4001, $"Paging test {index:00}", "Release notes")));
        await context.SaveChangesAsync();
        var specification = new NewslettersFilterSpecification(
            new NewsletterQuery(Page: 2, PageSize: 5, Search: "Paging test", SortBy: "title", SortDir: "asc"), 5);
        var repository = new NewsletterRepository(context);

        Assert.Equal(12, await repository.CountAsync(specification));
        Assert.Equal(new[] { "Paging test 06", "Paging test 07", "Paging test 08", "Paging test 09", "Paging test 10" },
            (await repository.ListAsync(specification)).Select(value => value.Title));
    }

    private static async Task<NewslettersDbContext> OpenContextAsync(QueryCounter? commands = null)
    {
        var options = new DbContextOptionsBuilder<NewslettersDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("NEWSLETTERS_TEST_CONNECTION"));
        if (commands is not null)
            options.AddInterceptors(commands);
        var context = new NewslettersDbContext(options.Options);
        await context.Database.MigrateAsync();
        await DbInitializer.SeedAsync(context);
        return context;
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}
