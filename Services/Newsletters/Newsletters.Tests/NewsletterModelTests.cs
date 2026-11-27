using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Newsletters.Dal.Data;
using Newsletters.Domain.Models;
using Xunit;

namespace Newsletters.Tests;

public sealed class NewsletterModelTests
{
    private static NewslettersDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NewslettersDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;

        return new NewslettersDbContext(options);
    }

    [Fact]
    public void Model_maps_newsletter_entities_to_the_project_two_schema()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(Newsletter));

        Assert.NotNull(entity);
        Assert.Equal("newsletters", entity.GetTableName());
        Assert.Equal("newsletter", entity.GetSchema());
        Assert.Equal("newsletter_id", entity.FindProperty(nameof(Newsletter.Id))!.GetColumnName());
    }

    [Fact]
    public void Model_maps_delivery_settings_as_one_to_one_and_subscription_as_join_entity()
    {
        using var context = CreateContext();
        var settings = context.Model.FindEntityType(typeof(NewsletterDeliverySettings));
        var subscription = context.Model.FindEntityType(typeof(Subscription));

        Assert.NotNull(settings);
        Assert.Equal("newsletter_delivery_settings", settings.GetTableName());
        Assert.Equal(nameof(NewsletterDeliverySettings.NewsletterId),
            Assert.Single(settings.GetKeys()).Properties.Single().Name);

        Assert.NotNull(subscription);
        Assert.Equal(new[] { nameof(Subscription.Id) },
            subscription.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Contains(subscription.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(Subscription.UserId), nameof(Subscription.NewsletterId) }));
    }

    [Fact]
    public void Public_feed_index_excludes_hidden_and_deleted_newsletters()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var newsletter = model.FindEntityType(typeof(Newsletter))!;
        var index = Assert.Single(newsletter.GetIndexes(), value =>
            value.GetDatabaseName() == "ix_newsletters_public_feed");

        Assert.Equal("is_hidden = FALSE AND is_deleted = FALSE", index.GetFilter());
    }
}
