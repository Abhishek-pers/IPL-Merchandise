using IplStore.Api.Composition;
using IplStore.Infrastructure;
using IplStore.Infrastructure.Options;
using Microsoft.Extensions.Options;

// ---------------------------------------------------------------------------------------------
// IPL Franchise Store - API host.
//   dotnet run                      -> serve HTTP (applies migrations first if configured)
//   dotnet run -- --migrate-only    -> apply migrations and exit (used by the CD pipeline job)
// ---------------------------------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIplStore(builder.Configuration);

var app = builder.Build();

var migrateOnly = args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase);
var database = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

if (migrateOnly || database.ApplyMigrationsOnStartup)
{
    await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);
}

if (migrateOnly)
{
    app.Logger.LogInformation("Migrations applied; exiting (--migrate-only).");
    return;
}

app.UseIplStorePipeline();

await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program
{
}
