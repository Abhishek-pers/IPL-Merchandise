using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace IplStore.IntegrationTests.Infrastructure;

/// <summary>Hosts the real API (all middleware, DI, EF Core) against a fresh test database.</summary>
public sealed class IplStoreApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public IplStoreApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ConnectionString", _connectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Database:SeedDemoData", "true");
        builder.UseSetting("RateLimiting:Enabled", "false");
    }
}

/// <summary>Base class: one database + one API host per test class, plus SQL/HTTP helpers.</summary>
[Collection(PostgresCollection.Name)]
public abstract class ApiTestBase : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgresFixture _postgres;

    protected ApiTestBase(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    protected IplStoreApiFactory Factory { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    protected string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        ConnectionString = await _postgres.CreateDatabaseAsync();
        Factory = new IplStoreApiFactory(ConnectionString);
        Client = Factory.CreateClient(); // builds the host -> runs migrations + seed
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    protected static HttpRequestMessage Request(HttpMethod method, string url, Guid? customerId = null, object? body = null, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (customerId is not null)
        {
            request.Headers.Add("X-Customer-Id", customerId.ToString());
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        return request;
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    protected async Task<Guid> CreateCustomerAsync()
    {
        var id = Guid.NewGuid();
        await ExecuteAsync($"INSERT INTO customers (id, email, full_name) VALUES ('{id}', '{id:N}@test.local', 'Test {id:N}')");
        return id;
    }

    protected async Task<Guid> ProductIdAsync(string sku) =>
        await ScalarAsync<Guid>($"SELECT id FROM products WHERE sku = '{sku}'");

    protected Task SetStockAsync(Guid productId, int stock) =>
        ExecuteAsync($"UPDATE products SET stock_quantity = {stock} WHERE id = '{productId}'");

    protected async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    protected async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is T typed
            ? typed
            : (T)Convert.ChangeType(value!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
}
