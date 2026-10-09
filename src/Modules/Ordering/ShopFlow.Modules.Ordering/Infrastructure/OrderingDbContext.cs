using Microsoft.EntityFrameworkCore;
using ShopFlow.BuildingBlocks.Messaging;
using ShopFlow.Modules.Ordering.Domain;

namespace ShopFlow.Modules.Ordering.Infrastructure;

internal class OrderingDbContext : DbContext, IHasOutbox
{
    public OrderingDbContext(DbContextOptions<OrderingDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ordering");

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("orders");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            b.Property(x => x.ReferenceId).HasColumnName("reference_id").IsRequired();
            b.Property(x => x.Status).HasColumnName("status").IsRequired();
            b.Property(x => x.TotalAmount).HasColumnName("total_amount").IsRequired();
            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.OrderId);
        });

        modelBuilder.Entity<OrderItem>(b =>
        {
            b.ToTable("order_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
            b.Property(x => x.SkuId).HasColumnName("sku_id").IsRequired();
            b.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
            b.Property(x => x.UnitPrice).HasColumnName("unit_price").IsRequired();
            b.Property(x => x.TotalPrice).HasColumnName("total_price").IsRequired();
        });

        modelBuilder.ApplyOutboxConfiguration();
    }
}
