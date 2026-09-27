using System.ComponentModel.DataAnnotations;

namespace IplStore.Application.Catalog;

/// <summary>Catalogue search limits. Bound from the "Catalog" configuration section.</summary>
public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    /// <summary>Longer search strings are rejected (protects the trigram index from abuse).</summary>
    [Range(1, 500)]
    public int MaxSearchLength { get; set; } = 100;

    /// <summary>Maximum number of whitespace-separated search terms that are applied.</summary>
    [Range(1, 20)]
    public int MaxSearchTerms { get; set; } = 5;

    /// <summary>Maximum number of franchise / category codes in a single filter.</summary>
    [Range(1, 100)]
    public int MaxFilterValues { get; set; } = 20;
}
