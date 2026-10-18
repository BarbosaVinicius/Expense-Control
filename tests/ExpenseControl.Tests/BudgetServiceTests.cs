using ExpenseControl.Core.Services;
using ExpenseControl.Tests.Fakes;

namespace ExpenseControl.Tests;

public class BudgetServiceTests
{
    private readonly InMemoryBudgetRepository _repository = new();
    private readonly BudgetService _budgets;

    public BudgetServiceTests()
    {
        _budgets = new BudgetService(_repository);
    }

    [Fact]
    public async Task Setting_a_budget_creates_it_and_setting_it_again_replaces_it()
    {
        Assert.True((await _budgets.SetBudgetAsync("maria", "food", 500m)).Succeeded);
        Assert.True((await _budgets.SetBudgetAsync("maria", "food", 650.50m)).Succeeded);

        var stored = Assert.Single(await _budgets.GetBudgetsAsync("maria"));
        Assert.Equal(("food", 650.50m), (stored.Category, stored.Amount));
    }

    [Fact]
    public async Task A_zero_amount_removes_the_budget()
    {
        await _budgets.SetBudgetAsync("maria", "food", 500m);

        Assert.True((await _budgets.SetBudgetAsync("maria", "food", 0m)).Succeeded);

        Assert.Empty(await _budgets.GetBudgetsAsync("maria"));
    }

    [Theory]
    [InlineData("salary", 100)]      // an income category
    [InlineData("nonsense", 100)]
    [InlineData("food", -1)]
    [InlineData("food", 1.005)]
    [InlineData("food", 100000000)]
    public async Task Invalid_budgets_are_rejected(string category, double amount)
    {
        var result = await _budgets.SetBudgetAsync("maria", category, (decimal)amount);

        Assert.False(result.Succeeded);
        Assert.StartsWith("error.", result.Error);
        Assert.Empty(await _budgets.GetBudgetsAsync("maria"));
    }

    [Fact]
    public async Task Budgets_belong_to_each_user()
    {
        await _budgets.SetBudgetAsync("maria", "food", 500m);
        await _budgets.SetBudgetAsync("joao", "food", 100m);

        Assert.Equal(500m, Assert.Single(await _budgets.GetBudgetsAsync("maria")).Amount);
        Assert.Equal(100m, Assert.Single(await _budgets.GetBudgetsAsync("joao")).Amount);
    }
}
