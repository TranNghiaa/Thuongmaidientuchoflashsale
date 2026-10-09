using Microsoft.EntityFrameworkCore;
using ShopFlow.Modules.Inventory.Domain;

namespace ShopFlow.Modules.Inventory.Infrastructure;

internal class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inventory");

        modelBuilder.Entity<InventoryItem>(b =>
        {
            b.ToTable("inventory_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.SkuId).HasColumnName("sku_id").IsRequired();
            b.HasIndex(x => x.SkuId).IsUnique();
            b.Property(x => x.AvailableQuantity).HasColumnName("available_quantity").IsRequired();
            b.Property(x => x.ReservedQuantity).HasColumnName("reserved_quantity").IsRequired();
            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            
            b.ToTable(t => t.HasCheckConstraint("CK_Inventory_Available", "available_quantity >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_Inventory_Reserved", "reserved_quantity >= 0"));
            b.Property<uint>("Version").IsRowVersion();
        });

        modelBuilder.Entity<Reservation>(b =>
        {
            b.ToTable("reservations");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.ReferenceId).HasColumnName("reference_id").IsRequired();
            b.HasIndex(x => x.ReferenceId).IsUnique();
            b.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            b.Property(x => x.Status).HasColumnName("status").IsRequired();
            
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ReservationId);
        });

        modelBuilder.Entity<ReservationItem>(b =>
        {
            b.ToTable("reservation_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.ReservationId).HasColumnName("reservation_id").IsRequired();
            b.Property(x => x.SkuId).HasColumnName("sku_id").IsRequired();
            b.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
        });
    }
}
