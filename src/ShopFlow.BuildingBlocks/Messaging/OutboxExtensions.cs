using Microsoft.EntityFrameworkCore;

namespace ShopFlow.BuildingBlocks.Messaging;

public static class OutboxExtensions
{
    public static void ApplyOutboxConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Type).HasColumnName("type").IsRequired();
            b.Property(x => x.Content).HasColumnName("content").IsRequired().HasColumnType("jsonb");
            b.Property(x => x.OccurredOn).HasColumnName("occurred_on").IsRequired();
            b.Property(x => x.ProcessedOn).HasColumnName("processed_on");
            b.Property(x => x.Error).HasColumnName("error");
        });
    }
}
