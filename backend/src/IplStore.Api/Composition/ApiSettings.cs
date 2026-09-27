using System.ComponentModel.DataAnnotations;

namespace IplStore.Api.Composition;

/// <summary>Allowed browser origins. Bound from the "Cors" section.</summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Per-client fixed-window rate limit (per customer header, else per IP). Protects the
/// database from a single abusive client. Bound from the "RateLimiting" section.
/// </summary>
public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    [Range(1, 100_000)]
    public int PermitLimit { get; set; } = 100;

    [Range(1, 3_600)]
    public int WindowSeconds { get; set; } = 10;

    [Range(0, 10_000)]
    public int QueueLimit { get; set; }
}
