using Microsoft.EntityFrameworkCore;
using StripePaymentDemo.Domain.Entities;

namespace StripePaymentDemo.Infrastructure.Persistence;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(
        DbContextOptions<PaymentDbContext> options)
    : base(options)
    {
        
    }
    
    public DbSet<Payment> Payments => Set<Payment>();
    
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount)
                .HasPrecision(18, 2);
            
            entity.Property(p => p.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(p => p.Status)
                .HasConversion<string>();
            
            entity.HasIndex(p => p.StripeCheckoutSessionId)
                .IsUnique()
                .HasFilter("[StripeCheckoutSessionId] IS NOT NULL");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            
            entity.Property(p => p.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(p => p.Price)
                .HasPrecision(18, 2);

            entity.Property(p => p.Currency)
                .HasMaxLength(3)
                .IsRequired();
        });
    }
}