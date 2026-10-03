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
        builder.Property(o => o.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(o => o.Subtotal).HasPrecision(12, 2);
        builder.Property(o => o.Tax).HasPrecision(12, 2);
        builder.Property(o => o.Shipping).HasPrecision(12, 2);
        builder.Property(o => o.Total).HasPrecision(12, 2);
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
