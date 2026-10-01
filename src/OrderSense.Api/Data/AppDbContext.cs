using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data.Entities;

namespace OrderSense.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();
    public DbSet<GeoZip> GeoZips => Set<GeoZip>();
    
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<BatchJob> BatchJobs => Set<BatchJob>();
    public DbSet<BatchResult> BatchResults => Set<BatchResult>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<AppUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        
        builder.Entity<Order>()
            .HasOne(o => o.Customer)
            .WithOne(c => c.Order)
            .HasForeignKey<Order>(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OrderItem>(e =>
        {
            e.HasOne(i => i.Product).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.Seller).WithMany().OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Prediction>(e =>
        {
            e.HasOne(p => p.ModelVersion).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.OwnsMany(p => p.Factors, f => f.ToJson());
            e.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        });

        builder.Entity<BatchResult>()
            .OwnsMany(r => r.Factors, f => f.ToJson());

        builder.Entity<BatchJob>(e =>
        {
            e.HasOne<AppUser>().WithMany().HasForeignKey(j => j.UserId);
            e.Property(j => j.CreatedAt).HasDefaultValueSql("now()");
        });

        builder.Entity<DeviceToken>(e =>
        {
            e.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId);
            e.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        });
    }
}