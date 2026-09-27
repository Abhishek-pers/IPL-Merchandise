using System.Text.Json;

namespace IplStore.Application.Catalog;

/// <summary>Row on the product list / search page.</summary>
public sealed record ProductSummaryDto(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    string Currency,
    bool InStock,
    string FranchiseCode,
    string FranchiseName,
    string FranchiseColor,
    string CategoryCode,
    string CategoryName,
    string? ImageUrl);

/// <summary>Everything the product details page shows.</summary>
public sealed record ProductDetailsDto(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    int StockQuantity,
    bool InStock,
    string FranchiseCode,
    string FranchiseName,
    string FranchiseColor,
    string CategoryCode,
    string CategoryName,
    string? ImageUrl,
    IReadOnlyDictionary<string, JsonElement> Attributes);

public sealed record FranchiseDto(Guid Id, string Code, string Name, string City, string PrimaryColor);

public sealed record CategoryDto(Guid Id, string Code, string Name);
