using Microsoft.EntityFrameworkCore;
using Newsletters.Domain.Models;

namespace Newsletters.Dal.Data;

public sealed class NewslettersDbContext(DbContextOptions<NewslettersDbContext> options) : DbContext(options)
{
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Newsletter> Newsletters => Set<Newsletter>();
    public DbSet<NewsletterDeliverySettings> DeliverySettings => Set<NewsletterDeliverySettings>();
    public DbSet<UserCopy> UsersCopy => Set<UserCopy>();
    public DbSet<UserComment> UserComments => Set<UserComment>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("newsletter");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NewslettersDbContext).Assembly);
    }
}
