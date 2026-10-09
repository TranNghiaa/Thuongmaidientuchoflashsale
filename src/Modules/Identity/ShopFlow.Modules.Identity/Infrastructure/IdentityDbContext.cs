using Microsoft.EntityFrameworkCore;
using ShopFlow.Modules.Identity.Domain;

namespace ShopFlow.Modules.Identity.Infrastructure;

internal class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Email).HasColumnName("email").IsRequired();
            b.HasIndex(x => x.Email).IsUnique();
            b.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
            b.Property(x => x.Role).HasColumnName("role").IsRequired();
            b.ToTable(t => t.HasCheckConstraint("CK_User_Role", "role IN ('User', 'Admin')"));
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.ToTable("refresh_tokens");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            b.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            b.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            b.HasIndex(x => x.UserId);
        });
    }
}
