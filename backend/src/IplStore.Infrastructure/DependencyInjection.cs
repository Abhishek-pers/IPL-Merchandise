using IplStore.Application.Carts;
using IplStore.Application.Catalog;
using IplStore.Application.Common;
using IplStore.Application.Customers;
using IplStore.Application.Orders;
using IplStore.Infrastructure.Options;
using IplStore.Infrastructure.Orders;
using IplStore.Infrastructure.Persistence;
using IplStore.Infrastructure.Persistence.Migrations;
using IplStore.Infrastructure.Queries;
using IplStore.Infrastructure.Queries.CatalogFilters;
using IplStore.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IplStore.Infrastructure;

/// <summary>
/// Registers every adapter for the Application ports. This is the single place to swap a
/// technology (e.g. Dapper for CatalogQueries, Redis for carts) - callers never change.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<StoreDbContext>((sp, builder) => ConfigureNpgsql(sp, builder, useReadReplica: false));
        services.AddDbContext<ReadOnlyStoreDbContext>((sp, builder) => ConfigureNpgsql(sp, builder, useReadReplica: true));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();

        // Write side (primary).
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        // Read side.
        services.AddScoped<ICatalogQueries, CatalogQueries>();
        services.AddScoped<ICartQueries, CartQueries>();
        services.AddScoped<IOrderQueries, OrderQueries>();

        // Search pipeline: order of registration = order of application. Add new filters here.
        services.AddSingleton<ICatalogFilter, SearchTermFilter>();
        services.AddSingleton<ICatalogFilter, FranchiseFilter>();
        services.AddSingleton<ICatalogFilter, CategoryFilter>();
        services.AddSingleton<ICatalogFilter, PriceRangeFilter>();
        services.AddSingleton<ICatalogFilter, InStockFilter>();

        services.AddSingleton<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddSingleton<IDatabaseMigrator, SqlScriptDatabaseMigrator>();

        return services;
    }

    /// <summary>Runs pending migrations (and demo seed if enabled). Safe to call from N instances.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>().MigrateAsync(cancellationToken);
    }

    private static void ConfigureNpgsql(IServiceProvider sp, DbContextOptionsBuilder builder, bool useReadReplica)
    {
        var database = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var resilience = sp.GetRequiredService<IOptions<ResilienceOptions>>().Value;

        var connectionString = useReadReplica && !string.IsNullOrWhiteSpace(database.ReadReplicaConnectionString)
            ? database.ReadReplicaConnectionString
            : database.ConnectionString;

        builder
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.CommandTimeout(database.CommandTimeoutSeconds);

                // Retry transient failures with exponential backoff (see ResilienceOptions).
                npgsql.EnableRetryOnFailure(
                    resilience.MaxRetryCount,
                    TimeSpan.FromMilliseconds(resilience.MaxRetryDelayMilliseconds),
                    resilience.AdditionalTransientErrorCodes);
            })
            .UseSnakeCaseNamingConvention();

        if (useReadReplica)
        {
            builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }

        if (database.EnableSensitiveDataLogging)
        {
            builder.EnableSensitiveDataLogging();
        }
    }
}
