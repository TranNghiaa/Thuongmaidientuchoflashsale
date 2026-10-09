using Microsoft.EntityFrameworkCore;
using ShopFlow.Modules.Payment.Domain;

namespace ShopFlow.Modules.Payment.Infrastructure;

internal class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<PaymentTransaction> Transactions => Set<PaymentTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payment");

        modelBuilder.Entity<PaymentTransaction>(b =>
        {
            b.ToTable("transactions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.ReferenceId).HasColumnName("reference_id").IsRequired();
            b.Property(x => x.Amount).HasColumnName("amount").IsRequired();
            b.Property(x => x.Status).HasColumnName("status").IsRequired();
            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        });
    }
}
