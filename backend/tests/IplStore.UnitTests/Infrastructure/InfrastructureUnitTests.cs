using System.Text.RegularExpressions;
using IplStore.Infrastructure.Orders;
using IplStore.Infrastructure.Persistence.Migrations;
using IplStore.Infrastructure.Queries.CatalogFilters;
using IplStore.UnitTests.Fakes;

namespace IplStore.UnitTests.Infrastructure;

public sealed class InfrastructureUnitTests
{
    [Fact]
    public void Like_wildcards_in_user_input_are_escaped()
    {
        SearchTermFilter.EscapeLikeWildcards(@"50%_off\").Should().Be(@"50\%\_off\\");
    }

    [Fact]
    public void Order_numbers_have_the_expected_shape_and_do_not_collide()
    {
        var generator = new OrderNumberGenerator();

        var numbers = Enumerable.Range(0, 10_000).Select(_ => generator.Next(TestData.Now)).ToList();

        numbers.Should().OnlyContain(n => Regex.IsMatch(n, "^IPL-20260924-[A-HJ-NP-Z2-9]{8}$"));
        numbers.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Migration_scripts_are_embedded_and_ordered()
    {
        var scripts = SqlScriptDatabaseMigrator.LoadScripts("migrations/");

        scripts.Select(s => s.Name).Should().StartWith(new[] { "V001__write_model.sql", "V002__catalog_read_model.sql", "V003__reference_data.sql" });
        scripts.Should().OnlyContain(s => s.Checksum.Length == 64 && !s.Sql.Contains('\r'));
    }

    [Fact]
    public void Demo_seed_scripts_are_embedded()
    {
        SqlScriptDatabaseMigrator.LoadScripts("seed/").Should().NotBeEmpty();
    }
}
