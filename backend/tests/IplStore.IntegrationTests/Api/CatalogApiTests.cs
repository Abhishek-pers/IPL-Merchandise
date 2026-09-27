using System.Net;
using IplStore.Application.Catalog;
using IplStore.Application.Common;
using IplStore.IntegrationTests.Infrastructure;

namespace IplStore.IntegrationTests.Api;

public sealed class CatalogApiTests : ApiTestBase
{
    public CatalogApiTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Fact]
    public async Task Product_list_shows_every_seeded_product_with_prices_and_paging()
    {
        var page = await ReadAsync<PagedResult<ProductSummaryDto>>(await Client.GetAsync("/api/v1/products?pageSize=5"));

        page.TotalCount.Should().Be(60);
        page.Items.Should().HaveCount(5).And.OnlyContain(p => p.Price > 0 && p.Currency == "INR");
        page.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task Search_filters_by_franchise_and_type()
    {
        var page = await ReadAsync<PagedResult<ProductSummaryDto>>(
            await Client.GetAsync("/api/v1/products?franchise=MI&category=JERSEY"));

        page.Items.Should().HaveCount(2).And.OnlyContain(p => p.FranchiseCode == "MI" && p.CategoryCode == "JERSEY");
    }

    [Fact]
    public async Task Free_text_search_matches_across_name_franchise_and_type()
    {
        var page = await ReadAsync<PagedResult<ProductSummaryDto>>(await Client.GetAsync("/api/v1/products?search=chennai%20team%20cap"));

        page.Items.Should().ContainSingle().Which.Sku.Should().Be("CSK-CAP");
    }

    [Fact]
    public async Task Sorting_by_price_descending_puts_autographed_photos_first()
    {
        var page = await ReadAsync<PagedResult<ProductSummaryDto>>(
            await Client.GetAsync("/api/v1/products?sort=PriceHighToLow&pageSize=10"));

        page.Items.Should().BeInDescendingOrder(p => p.Price);
        page.Items.First().CategoryCode.Should().Be("AUTOGRAPHED_PHOTO");
    }

    [Fact]
    public async Task Product_details_include_attributes()
    {
        var id = await ProductIdAsync("RCB-JER-H");

        var details = await ReadAsync<ProductDetailsDto>(await Client.GetAsync($"/api/v1/products/{id}"));

        details.Name.Should().Contain("Royal Challengers");
        details.Attributes.Should().ContainKey("sizes");
    }

    [Fact]
    public async Task Unknown_product_returns_problem_details_404()
    {
        var response = await Client.GetAsync($"/api/v1/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("product.not_found");
    }

    [Fact]
    public async Task Invalid_price_range_is_a_400()
    {
        var response = await Client.GetAsync("/api/v1/products?minPrice=900&maxPrice=100");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Renaming_a_franchise_is_reflected_in_the_denormalised_catalogue()
    {
        await ExecuteAsync("UPDATE franchises SET name = 'Gujarat Titans FC' WHERE code = 'GT'");

        var page = await ReadAsync<PagedResult<ProductSummaryDto>>(await Client.GetAsync("/api/v1/products?franchise=GT"));

        page.Items.Should().OnlyContain(p => p.FranchiseName == "Gujarat Titans FC");
    }
}
