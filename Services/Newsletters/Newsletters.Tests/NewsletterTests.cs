using Newsletters.Domain.Models;
using Xunit;

namespace Newsletters.Tests;

public sealed class NewsletterTests
{
    [Fact]
    public void Publish_sets_publication_metadata_and_makes_newsletter_visible()
    {
        var newsletter = new Newsletter(4001, "Development Update", "Release notes");
        var publishedAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        newsletter.Publish("editor_demo", publishedAt);

        Assert.False(newsletter.IsHidden);
        Assert.Equal(publishedAt, newsletter.PublishedAt);
        Assert.Equal("editor_demo", newsletter.PublishedBy);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void Constructor_rejects_invalid_title_length(int titleLength)
    {
        var title = new string('x', titleLength);

        Assert.Throws<ArgumentException>(() => new Newsletter(4001, title, "Release notes"));
    }
}
