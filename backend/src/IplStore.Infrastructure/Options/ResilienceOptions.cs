using System.ComponentModel.DataAnnotations;

namespace IplStore.Infrastructure.Options;

/// <summary>
/// Retry behaviour for database work. Bound from the "Resilience" configuration section.
/// </summary>
/// <remarks>
/// Transactions are retried as a WHOLE by EF Core's Npgsql execution strategy (exponential
/// backoff with jitter) when PostgreSQL reports a transient error: connection loss, deadlock
/// (40P01), serialization failure (40001), server shutting down (57P0x), out of resources (53xxx).
/// Business errors (insufficient stock ...) are never retried.
/// </remarks>
public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";

    /// <summary>Retries AFTER the first attempt. 0 disables retries.</summary>
    [Range(0, 10)]
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>Upper bound for a single backoff delay.</summary>
    [Range(10, 60_000)]
    public int MaxRetryDelayMilliseconds { get; set; } = 2_000;

    /// <summary>Extra PostgreSQL SQLSTATE codes to treat as transient (e.g. "55P03" lock_not_available).</summary>
    public string[] AdditionalTransientErrorCodes { get; set; } = Array.Empty<string>();

    /// <summary>How many times start-up migration waits for the database to become reachable.</summary>
    [Range(1, 100)]
    public int StartupConnectAttempts { get; set; } = 15;

    [Range(100, 60_000)]
    public int StartupRetryDelayMilliseconds { get; set; } = 2_000;
}
