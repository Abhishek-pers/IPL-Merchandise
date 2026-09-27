using IplStore.Domain.Carts;
using IplStore.Domain.Catalog;
using IplStore.Domain.Customers;
using IplStore.Domain.Orders;
using IplStore.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Persistence;

/// <summary>
/// EF Core context bound to the PRIMARY database. Used for every command and for queries
/// that need read-your-writes (cart, order history).
/// </summary>
/// <remarks>
/// The schema is owned by the SQL scripts in /database/migrations, NOT by EF migrations:
/// this context only MAPS to existing tables (see Configurations/*). That keeps DDL reviewable
/// by a DBA, lets us use PostgreSQL features EF can't model (triggers, generated columns,
/// partial/trigram indexes) and makes the ORM swappable (e.g. Dapper for hot paths).
/// </remarks>
public class StoreDbContext : DbContext
{
    public StoreDbContext(DbContextOptions<StoreDbContext> options)
        : base(options)
    {
    }

    /// <summary>For derived contexts (see <see cref="ReadOnlyStoreDbContext"/>).</summary>
    protected StoreDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<Franchise> Franchises => Set<Franchise>();

    public DbSet<ProductCategory> Categories => Set<ProductCategory>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    /// <summary>Denormalised read model (trigger-maintained).</summary>
    public DbSet<CatalogItem> Catalog => Set<CatalogItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StoreDbContext).Assembly);
    }
}

/// <summary>
/// Same model, but pointed at the READ REPLICA (when configured) with change tracking off.
/// Saving is forbidden so a write can never accidentally go to a replica.
/// </summary>
public sealed class ReadOnlyStoreDbContext : StoreDbContext
{
    public ReadOnlyStoreDbContext(DbContextOptions<ReadOnlyStoreDbContext> options)
        : base(options)
    {
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new InvalidOperationException("ReadOnlyStoreDbContext cannot save changes.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("ReadOnlyStoreDbContext cannot save changes.");
}
