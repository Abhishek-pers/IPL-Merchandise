using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using IplStore.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace IplStore.Infrastructure.Persistence.Migrations;

/// <summary>Applies the versioned SQL scripts in /database to the target database.</summary>
public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Minimal Flyway-style migrator (no extra dependency):
/// <list type="bullet">
///   <item>scripts are embedded resources, applied in ordinal name order (V001, V002 ...);</item>
///   <item>each script runs in its own transaction and is recorded in <c>schema_migrations</c>;</item>
///   <item>a SHA-256 checksum detects edits to already-applied scripts (they are immutable);</item>
///   <item>a PostgreSQL advisory lock makes it safe when N replicas start at the same time -
///         one migrates, the others wait and then find nothing to do;</item>
///   <item>start-up waits (with retries) for the database to accept connections.</item>
/// </list>
/// </summary>
internal sealed class SqlScriptDatabaseMigrator : IDatabaseMigrator
{
    /// <summary>Arbitrary but fixed application-wide lock id.</summary>
    private const long AdvisoryLockKey = 7_301_945_2026;

    private const string MigrationsPrefix = "migrations/";
    private const string SeedPrefix = "seed/";

    private const string CreateHistoryTableSql = """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            script_name varchar(200) PRIMARY KEY,
            checksum    char(64)     NOT NULL,
            applied_at  timestamptz  NOT NULL DEFAULT now()
        );
        """;

    private readonly DatabaseOptions _database;
    private readonly ResilienceOptions _resilience;
    private readonly ILogger<SqlScriptDatabaseMigrator> _logger;

    public SqlScriptDatabaseMigrator(
        IOptions<DatabaseOptions> database,
        IOptions<ResilienceOptions> resilience,
        ILogger<SqlScriptDatabaseMigrator> logger)
    {
        _database = database.Value;
        _resilience = resilience.Value;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await MigrateOnceAsync(cancellationToken);
                return;
            }
            catch (NpgsqlException ex) when (ex.IsTransient && attempt < _resilience.StartupConnectAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Database not reachable yet (attempt {Attempt}/{MaxAttempts}); retrying in {Delay} ms",
                    attempt,
                    _resilience.StartupConnectAttempts,
                    _resilience.StartupRetryDelayMilliseconds);
                await Task.Delay(_resilience.StartupRetryDelayMilliseconds, cancellationToken);
            }
        }
    }

    internal static IReadOnlyList<SqlScript> LoadScripts(string prefix)
    {
        var assembly = typeof(SqlScriptDatabaseMigrator).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => SqlScript.FromResource(assembly, n, prefix))
            .ToList();
    }

    private async Task MigrateOnceAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, null, $"SELECT pg_advisory_lock({AdvisoryLockKey})", cancellationToken);
        try
        {
            await ExecuteAsync(connection, null, CreateHistoryTableSql, cancellationToken);
            var applied = await LoadAppliedAsync(connection, cancellationToken);

            foreach (var script in LoadScripts(MigrationsPrefix))
            {
                if (applied.TryGetValue(script.Name, out var checksum))
                {
                    if (!string.Equals(checksum.Trim(), script.Checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Migration '{script.Name}' was modified after it was applied. Applied scripts are immutable - add a new script instead.");
                    }

                    continue;
                }

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await ExecuteAsync(connection, transaction, script.Sql, cancellationToken);
                await using (var record = new NpgsqlCommand(
                                 "INSERT INTO schema_migrations (script_name, checksum) VALUES (@name, @checksum)",
                                 connection,
                                 transaction))
                {
                    record.Parameters.AddWithValue("name", script.Name);
                    record.Parameters.AddWithValue("checksum", script.Checksum);
                    await record.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Applied migration {Script}", script.Name);
            }

            if (_database.SeedDemoData)
            {
                // Seed scripts are idempotent (ON CONFLICT DO NOTHING) and re-run on every start.
                foreach (var seed in LoadScripts(SeedPrefix))
                {
                    await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                    await ExecuteAsync(connection, transaction, seed.Sql, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    _logger.LogInformation("Applied seed {Script}", seed.Name);
                }
            }
        }
        finally
        {
            await ExecuteAsync(connection, null, $"SELECT pg_advisory_unlock({AdvisoryLockKey})", CancellationToken.None);
        }
    }

    private static async Task<Dictionary<string, string>> LoadAppliedAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var applied = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = new NpgsqlCommand("SELECT script_name, checksum FROM schema_migrations", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied[reader.GetString(0)] = reader.GetString(1);
        }

        return applied;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.CommandTimeout = 300;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal sealed record SqlScript(string Name, string Sql, string Checksum)
    {
        public static SqlScript FromResource(Assembly assembly, string resourceName, string prefix)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded script '{resourceName}' not found.");
            using var reader = new StreamReader(stream, Encoding.UTF8);

            // Normalise line endings so a Windows (CRLF) checkout and a Linux (LF) build agent
            // compute the same checksum for the same script.
            var sql = reader.ReadToEnd().ReplaceLineEndings("\n");
            var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
            return new SqlScript(resourceName[prefix.Length..], sql, checksum);
        }
    }
}
