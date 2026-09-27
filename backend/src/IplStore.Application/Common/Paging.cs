using System.ComponentModel.DataAnnotations;

namespace IplStore.Application.Common;

/// <summary>Tunable paging defaults. Bound from the "Paging" configuration section.</summary>
public sealed class PagingOptions
{
    public const string SectionName = "Paging";

    [Range(1, 500)]
    public int DefaultPageSize { get; set; } = 12;

    [Range(1, 500)]
    public int MaxPageSize { get; set; } = 100;
}

/// <summary>A normalised (always valid) page request.</summary>
public sealed record PageRequest(int Page, int PageSize)
{
    public int Offset => (Page - 1) * PageSize;

    /// <summary>
    /// Builds a valid page request from untrusted input: missing values get defaults,
    /// oversized pages are clamped instead of rejected (friendlier for clients).
    /// </summary>
    public static PageRequest Create(int? page, int? pageSize, PagingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (page is < 1)
        {
            throw new RequestValidationException(nameof(page), "Page must be 1 or greater.");
        }

        if (pageSize is < 1)
        {
            throw new RequestValidationException(nameof(pageSize), "Page size must be 1 or greater.");
        }

        var size = Math.Min(pageSize ?? options.DefaultPageSize, options.MaxPageSize);
        return new PageRequest(page ?? 1, size);
    }
}

/// <summary>One page of results plus the information a UI needs to render a pager.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => Page < TotalPages;
}
