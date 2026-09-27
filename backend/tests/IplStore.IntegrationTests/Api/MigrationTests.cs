using IplStore.Infrastructure;
using IplStore.IntegrationTests.Infrastructure;

namespace IplStore.IntegrationTests.Api;

public sealed class MigrationTests : ApiTestBase
{
    public MigrationTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task Migrations_are_idempotent_and_safe_to_run_from_many_instances_at_once()
    {
        // The host already migrated once. Simulate 5 replicas starting simultaneously.
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Factory.Services.MigrateDatabaseAsync()));

        (await ScalarAsync<long>("SELECT count(*) FROM schema_migrations")).Should().Be(4);
        (await ScalarAsync<long>("SELECT count(*) FROM products")).Should().Be(60, "seed scripts are idempotent");
        (await ScalarAsync<long>("SELECT count(*) FROM product_catalog")).Should().Be(60);
    }
}
