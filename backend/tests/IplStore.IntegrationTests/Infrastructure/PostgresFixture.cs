using Npgsql;
using Testcontainers.PostgreSql;

namespace IplStore.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL server for the whole test run (Testcontainers), one fresh DATABASE per test
/// class -> full isolation without paying container start-up per class.
/// Set IPLSTORE_TEST_POSTGRES (admin connection string) to reuse an existing server instead
/// of Docker, e.g. "Host=localhost;Username=postgres;Password=postgres".
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ExternalServerVariable = "IPLSTORE_TEST_POSTGRES";

    private PostgreSqlContainer? _container;

    public string AdminConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(ExternalServerVariable);
        if (!string.IsNullOrWhiteSpace(external))
        {
            AdminConnectionString = external;
            return;
        }

        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("postgres")
            .Build();
        await _container.StartAsync();
        AdminConnectionString = _container.GetConnectionString();
    }

    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"ipl_test_{Guid.NewGuid():N}"[..24];
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
        await command.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = name, MaxPoolSize = 100 }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}
