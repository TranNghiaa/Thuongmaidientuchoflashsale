using Microsoft.EntityFrameworkCore;
using ShopFlow.Modules.Catalog.Domain;

namespace ShopFlow.Modules.Catalog.Infrastructure;

internal class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<FlashSale> FlashSales => Set<FlashSale>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");

        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("products");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.SkuId).HasColumnName("sku_id").IsRequired();
            b.HasIndex(x => x.SkuId).IsUnique();
            b.Property(x => x.Name).HasColumnName("name").IsRequired();
            b.Property(x => x.Description).HasColumnName("description");
            b.Property(x => x.BasePrice).HasColumnName("base_price").IsRequired();
            b.ToTable(t => t.HasCheckConstraint("CK_Product_BasePrice", "base_price > 0"));
            b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        });

        modelBuilder.Entity<FlashSale>(b =>
        {
            b.ToTable("flash_sales");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.SkuId).HasColumnName("sku_id").IsRequired();
            b.Property(x => x.SalePrice).HasColumnName("sale_price").IsRequired();
            b.Property(x => x.StartsAt).HasColumnName("starts_at").IsRequired();
            b.Property(x => x.EndsAt).HasColumnName("ends_at").IsRequired();
            b.HasIndex(x => x.SkuId);
            b.ToTable(t => t.HasCheckConstraint("CK_FlashSale_SalePrice", "sale_price > 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_FlashSale_Time", "starts_at < ends_at"));
        });
    }
}
