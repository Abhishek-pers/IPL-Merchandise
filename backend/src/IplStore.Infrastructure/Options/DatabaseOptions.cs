using System.ComponentModel.DataAnnotations;

namespace IplStore.Infrastructure.Options;

/// <summary>Database connectivity. Bound from the "Database" configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>PRIMARY (read-write) connection. All commands and read-your-writes queries.</summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Optional READ REPLICA used for catalogue browsing. When empty, the primary is used.
    /// See docs/06-distributed-database.md.
    /// </summary>
    public string? ReadReplicaConnectionString { get; set; }

    /// <summary>Apply pending SQL migrations when the API starts (local/dev). CD runs `--migrate-only` instead.</summary>
    public bool ApplyMigrationsOnStartup { get; set; } = true;

    /// <summary>Load the demo catalogue and demo customers (never in production).</summary>
    public bool SeedDemoData { get; set; }

    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>Logs SQL parameter values. Development only.</summary>
    public bool EnableSensitiveDataLogging { get; set; }
}
