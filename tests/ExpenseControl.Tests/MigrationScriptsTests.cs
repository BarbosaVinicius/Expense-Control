using ExpenseControl.Infrastructure.Persistence;

namespace ExpenseControl.Tests;

public class MigrationScriptsTests
{
    [Fact]
    public void Scripts_are_embedded_and_ordered_so_that_Users_exists_before_the_tables_that_reference_it()
    {
        var versions = MigrationRunner.DiscoverScripts().Select(s => s.Version).ToList();

        Assert.Equal(
            [
                "001_create_users",
                "002_create_expenses",
                "003_create_amount_received",
                "004_add_expense_category",
                "005_add_income_category",
                "006_create_budgets"
            ],
            versions);
    }

    [Fact]
    public void Script_versions_are_unique_and_numbered()
    {
        var versions = MigrationRunner.DiscoverScripts().Select(s => s.Version).ToList();

        Assert.Equal(versions.Count, versions.Distinct().Count());
        Assert.All(versions, v => Assert.Matches(@"^\d{3}_[a-z0-9_]+$", v));
    }

    [Fact]
    public void Scripts_are_not_empty()
    {
        Assert.All(MigrationRunner.DiscoverScripts(), s => Assert.False(string.IsNullOrWhiteSpace(s.Sql)));
    }
}
