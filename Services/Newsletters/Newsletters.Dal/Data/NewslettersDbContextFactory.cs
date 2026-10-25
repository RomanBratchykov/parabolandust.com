using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Newsletters.Dal.Data;

public sealed class NewslettersDbContextFactory : IDesignTimeDbContextFactory<NewslettersDbContext>
{
    public NewslettersDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__NewslettersDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__NewslettersDb before creating or applying migrations.");
        }

        var options = new DbContextOptionsBuilder<NewslettersDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new NewslettersDbContext(options);
    }
}
