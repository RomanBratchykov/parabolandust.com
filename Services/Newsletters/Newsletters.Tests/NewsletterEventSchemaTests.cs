using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Newsletters.Dal.Data;
using Newsletters.Domain.Models;
using Xunit;

namespace Newsletters.Tests;

public sealed class NewsletterEventSchemaTests
{
    [Fact]
    public void An_inbox_entry_requires_the_original_event_identity() =>
        Assert.Throws<ArgumentException>(() => new ProcessedEvent(Guid.Empty));

    [Fact]
    public void Account_source_ordering_is_separate_from_the_local_concurrency_token()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var user = model.FindEntityType(typeof(UserCopy))!;
        var sourceVersion = user.FindProperty("SourceVersion");
        Assert.NotNull(sourceVersion);
        Assert.Equal(typeof(long), sourceVersion.ClrType);
        Assert.Equal("source_version", sourceVersion.GetColumnName());
        Assert.Equal(0L, sourceVersion.GetDefaultValue());
        Assert.False(sourceVersion.IsConcurrencyToken);
        Assert.True(user.FindProperty(nameof(UserCopy.RowVersion))!.IsConcurrencyToken);
        Assert.Equal("timestamp with time zone", user.FindProperty(nameof(UserCopy.SourceUpdatedAt))!.GetColumnType());
        Assert.Contains(user.GetCheckConstraints(), check =>
            check.Name == "ck_users_copy_source_version" && check.Sql == "source_version >= 0");
    }

    [Fact]
    public void Processed_events_have_a_unique_uuid_identity_and_database_processing_timestamp()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType("Newsletters.Domain.Models.ProcessedEvent");
        Assert.NotNull(entity);
        Assert.Equal("newsletter", entity.GetSchema());
        Assert.Equal("processed_events", entity.GetTableName());
        var id = Assert.Single(entity.FindPrimaryKey()!.Properties);
        Assert.Equal("EventId", id.Name);
        Assert.Equal(typeof(Guid), id.ClrType);
        Assert.Equal("event_id", id.GetColumnName());
        Assert.Equal("uuid", id.GetColumnType());
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
        var processedAt = entity.FindProperty("ProcessedAt")!;
        Assert.Equal("processed_at", processedAt.GetColumnName());
        Assert.Equal("timestamp with time zone", processedAt.GetColumnType());
        Assert.Equal("CURRENT_TIMESTAMP", processedAt.GetDefaultValueSql());
        Assert.Empty(entity.GetForeignKeys());
    }

    [Fact]
    public void Upgrade_sql_adds_the_inbox_and_initializes_legacy_versions_without_replacing_existing_data()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript("20261025193109_AlignForeignKeyDeleteBehavior");
        Assert.Contains("ADD source_version bigint NOT NULL DEFAULT 0", script);
        Assert.Contains("CHECK (source_version >= 0)", script);
        Assert.Contains("CREATE TABLE newsletter.processed_events", script);
        Assert.Contains("event_id uuid NOT NULL", script);
        Assert.Contains("processed_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP)", script);
        Assert.Contains("PRIMARY KEY (event_id)", script);
        Assert.DoesNotContain("DROP ", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER COLUMN", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE newsletter.", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE newsletter.subscriptions", script);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static NewslettersDbContext CreateContext() => new(new DbContextOptionsBuilder<NewslettersDbContext>()
        .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
}
