using IplStore.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IplStore.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.OrderNumber).HasMaxLength(32);
        builder.Property(o => o.IdempotencyKey).HasMaxLength(100);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Total).HasPrecision(12, 2);

        // Complex type: the PriceBreakdown value object lives in columns of the orders row
        // (no extra table). Explicit names keep the existing columns (not price_subtotal).
        builder.ComplexProperty(o => o.Price, price =>
        {
            price.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength();
            price.Property(p => p.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
            price.Property(p => p.Tax).HasColumnName("tax").HasPrecision(12, 2);
            price.Property(p => p.Shipping).HasColumnName("shipping").HasPrecision(12, 2);
        });
        builder.Property(o => o.CustomerName).HasMaxLength(150);
        builder.Property(o => o.CustomerEmail).HasMaxLength(254);

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
