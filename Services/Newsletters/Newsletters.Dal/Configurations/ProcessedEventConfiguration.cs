using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newsletters.Domain.Models;

namespace Newsletters.Dal.Configurations;

public sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("processed_events", "newsletter");
        builder.HasKey(value => value.EventId);
        builder.Property(value => value.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(value => value.ProcessedAt).HasColumnName("processed_at")
            .HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
