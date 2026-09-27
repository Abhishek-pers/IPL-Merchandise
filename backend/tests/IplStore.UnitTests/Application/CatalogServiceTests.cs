using IplStore.Application.Catalog;
using IplStore.Application.Common;

namespace IplStore.UnitTests.Application;

public sealed class CatalogServiceTests
{
    private static readonly CatalogOptions Options = new() { MaxSearchLength = 20, MaxSearchTerms = 2, MaxFilterValues = 3 };

    [Fact]
    public void Criteria_normalises_codes_from_csv_and_repeated_values()
    {
        var criteria = CatalogService.BuildCriteria(
            new SearchProductsQuery { Franchises = new[] { "csk, mi", "MI" }, Categories = new[] { "jersey" } },
            Options);

        criteria.FranchiseCodes.Should().BeEquivalentTo(new[] { "CSK", "MI" });
        criteria.CategoryCodes.Should().BeEquivalentTo(new[] { "JERSEY" });
    }

    [Fact]
    public void Criteria_splits_search_text_into_distinct_lower_case_terms_up_to_the_limit()
    {
        var criteria = CatalogService.BuildCriteria(new SearchProductsQuery { Search = "  MI  JERSEY mi cap " }, Options);

        criteria.SearchTerms.Should().Equal("mi", "jersey");
    }

    [Fact]
    public void Min_price_greater_than_max_price_is_a_validation_error()
    {
        var act = () => CatalogService.BuildCriteria(new SearchProductsQuery { MinPrice = 500, MaxPrice = 100 }, Options);

        act.Should().Throw<RequestValidationException>().Which.Errors.Should().ContainKey(nameof(SearchProductsQuery.MaxPrice));
    }

    [Fact]
    public void Too_long_search_text_is_a_validation_error()
    {
        var act = () => CatalogService.BuildCriteria(new SearchProductsQuery { Search = new string('x', 21) }, Options);

        act.Should().Throw<RequestValidationException>();
    }

    [Theory]
    [InlineData(null, null, 1, 12)]
    [InlineData(3, 500, 3, 100)]
    public void Page_request_applies_defaults_and_clamps_page_size(int? page, int? size, int expectedPage, int expectedSize)
    {
        var request = PageRequest.Create(page, size, new PagingOptions { DefaultPageSize = 12, MaxPageSize = 100 });

        request.Should().Be(new PageRequest(expectedPage, expectedSize));
    }
}
