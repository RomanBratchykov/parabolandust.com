using Microsoft.EntityFrameworkCore;
using Newsletters.Dal.Data;
using Newsletters.Domain.Models;

namespace Newsletters.Dal.Seeding;

public static class DbInitializer
{
    public static async Task SeedAsync(NewslettersDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Newsletters.AnyAsync(cancellationToken))
            return;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var newsletter = new Newsletter(4001, "Development Update",
            "A short update from the game development team.");
        newsletter.Publish("editor_demo", DateTimeOffset.UtcNow);
        await context.Newsletters.AddAsync(newsletter, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await context.DeliverySettings.AddAsync(
            new NewsletterDeliverySettings(newsletter.Id, "team@example.test"), cancellationToken);
        await context.Subscriptions.AddAsync(
            new Subscription(1001, newsletter.Id, "alex@example.test"), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
