using ExpenseControl.Core.Domain;
using ExpenseControl.Core.Services;
using ExpenseControl.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace ExpenseControl.Tests;

public class DashboardServiceTests
{
    private readonly InMemoryLedgerRepository _ledgerRepository = new();
    private readonly InMemoryBudgetRepository _budgetRepository = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2025, 3, 15, 10, 0, 0, TimeSpan.Zero));
    private readonly DashboardService _dashboard;

    public DashboardServiceTests()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _dashboard = new DashboardService(_ledgerRepository, _budgetRepository, _clock);
    }

    private void Add(EntryType type, string date, decimal value, string category = "other", string user = "maria") =>
        _ledgerRepository.Entries.Add(new LedgerEntry(
            type, user, "n", DateOnly.Parse(date), value, "d", category, _ledgerRepository.Entries.Count + 1));

    [Fact]
    public async Task Month_totals_balance_and_savings_rate()
    {
        Add(EntryType.Income, "2025-03-01", 4000m, "salary");
        Add(EntryType.Expense, "2025-03-05", 1000m, "housing");
        Add(EntryType.Expense, "2025-03-20", 1000m, "food");

        var data = await _dashboard.GetAsync("maria", new DateOnly(2025, 3, 20));

        Assert.Equal(new DateOnly(2025, 3, 1), data.Month);
        Assert.Equal(4000m, data.Current.Income);
        Assert.Equal(2000m, data.Current.Expenses);
        Assert.Equal(2000m, data.Current.Balance);
        Assert.Equal(0.5m, data.SavingsRate);
    }

    [Fact]
    public async Task Savings_rate_is_unknown_without_income_and_negative_when_overspent()
    {
        Add(EntryType.Expense, "2025-03-05", 100m);
        Assert.Null((await _dashboard.GetAsync("maria", new DateOnly(2025, 3, 1))).SavingsRate);

        Add(EntryType.Income, "2025-03-06", 50m);
        Assert.Equal(-1m, (await _dashboard.GetAsync("maria", new DateOnly(2025, 3, 1))).SavingsRate);
    }

    [Fact]
    public async Task Only_the_users_own_entries_are_counted()
    {
        Add(EntryType.Expense, "2025-03-05", 100m);
        Add(EntryType.Expense, "2025-03-05", 999m, user: "joao");

        var data = await _dashboard.GetAsync("maria", new DateOnly(2025, 3, 1));

        Assert.Equal(100m, data.Current.Expenses);
    }

    [Fact]
    public async Task Trend_covers_six_months_ending_at_the_selected_one_including_empty_months()
    {
        Add(EntryType.Expense, "2024-09-10", 10m);   // outside the window
        Add(EntryType.Expense, "2024-11-10", 20m);
        Add(EntryType.Income, "2025-01-10", 300m);
        Add(EntryType.Expense, "2025-03-10", 40m);

        var data = await _dashboard.GetAsync("maria", new DateOnly(2025, 3, 1));

        Assert.Equal(6, data.Trend.Count);
        Assert.Equal(new DateOnly(2024, 10, 1), data.Trend[0].Month);
        Assert.Equal(new DateOnly(2025, 3, 1), data.Trend[^1].Month);
        Assert.Equal([0m, 20m, 0m, 0m, 0m, 40m], data.Trend.Select(m => m.Expenses).ToArray());
        Assert.Equal([0m, 0m, 0m, 300m, 0m, 0m], data.Trend.Select(m => m.Income).ToArray());
    }

    [Fact]
    public async Task Previous_month_is_the_one_before_the_selected_month_even_across_years()
    {
        Add(EntryType.Expense, "2024-12-31", 70m);
        Add(EntryType.Expense, "2025-01-01", 30m);

        var data = await _dashboard.GetAsync("maria", new DateOnly(2025, 1, 15));

        Assert.Equal(new DateOnly(2024, 12, 1), data.Previous.Month);
        Assert.Equal(70m, data.Previous.Expenses);
        Assert.Equal(30m, data.Current.Expenses);
        Assert.Equal(-0.5714m, Math.Round(DashboardData.PercentChange(data.Previous.Expenses, data.Current.Expenses)!.Value, 4));
    }

    [Fact]
    public void Percent_change_is_unknown_without_a_base()
    {
        Assert.Null(DashboardData.PercentChange(0m, 50m));
        Assert.Equal(1m, DashboardData.PercentChange(50m, 100m));
    }

    [Fact]
    public async Task Expenses_by_category_are_sorted_with_shares_and_ignore_income()
    {
        Add(EntryType.Expense, "2025-03-02", 600m, "housing");
        Add(EntryType.Expense, "2025-03-03", 300m, "food");
        Add(EntryType.Expense, "2025-03-04", 100m, "food");
        Add(EntryType.Income, "2025-03-05", 5000m, "salary");

        var data = await _dashboard.GetAsync("maria", new DateOnly(2025, 3, 1));

        Assert.Collection(data.ExpensesByCategory,
            first => Assert.Equal(new CategoryTotal("housing", 600m, 0.6m), first),
            second => Assert.Equal(new CategoryTotal("food", 400m, 0.4m), second));
    }

    [Fact]
    public async Task Cumulative_expenses_stop_at_today_for_the_running_month()
    {
        Add(EntryType.Expense, "2025-03-02", 10m);
        Add(EntryType.Expense, "2025-03-02", 5m);
        Add(EntryType.Expense, "2025-03-10", 100m);
        Add(EntryType.Expense, "2025-03-25", 999m);   // in the future relative to the fake "today" (the 15th)

        var data = await _dashboard.GetAsync("maria", new DateOnly(2025, 3, 1));

        Assert.Equal(15, data.CumulativeExpenses.Count);
        Assert.Equal(0m, data.CumulativeExpenses[0].Cumulative);
        Assert.Equal(15m, data.CumulativeExpenses[1].Cumulative);
        Assert.Equal(115m, data.CumulativeExpenses[^1].Cumulative);
    }

    [Fact]
    public async Task Cumulative_expenses_cover_the_whole_month_for_past_months_and_none_for_future_ones()
    {
        Add(EntryType.Expense, "2025-02-28", 10m);

        var past = await _dashboard.GetAsync("maria", new DateOnly(2025, 2, 1));
        var future = await _dashboard.GetAsync("maria", new DateOnly(2025, 6, 1));

        Assert.Equal(28, past.CumulativeExpenses.Count);
        Assert.Equal(10m, past.CumulativeExpenses[^1].Cumulative);
        Assert.Empty(future.CumulativeExpenses);
    }

    [Fact]
    public async Task Budget_status_compares_the_months_spending_with_the_limit_and_sorts_by_usage()
    {
        await _budgetRepository.SetAsync("maria", new Budget("food", 500m));
        await _budgetRepository.SetAsync("maria", new Budget("housing", 1000m));
        await _budgetRepository.SetAsync("maria", new Budget("leisure", 100m));
        Add(EntryType.Expense, "2025-03-02", 600m, "food");
        Add(EntryType.Expense, "2025-03-03", 250m, "housing");
        Add(EntryType.Expense, "2025-02-03", 90m, "leisure");   // last month: does not count

        var data = await _dashboard.GetAsync("maria", new DateOnly(2025, 3, 1));

        Assert.Equal(["food", "housing", "leisure"], data.Budgets.Select(b => b.Category).ToArray());
        Assert.True(data.Budgets[0].IsOver);
        Assert.Equal(1.2m, data.Budgets[0].Ratio);
        Assert.Equal(-100m, data.Budgets[0].Remaining);
        Assert.Equal(0.25m, data.Budgets[1].Ratio);
        Assert.Equal(0m, data.Budgets[2].Spent);
    }

    [Fact]
    public async Task Recent_entries_are_the_latest_of_the_month_first()
    {
        for (var day = 1; day <= 8; day++)
        {
            Add(EntryType.Expense, $"2025-03-{day:00}", day);
        }

        Add(EntryType.Expense, "2025-02-28", 99m);

        var data = await _dashboard.GetAsync("maria", new DateOnly(2025, 3, 1));

        Assert.Equal(6, data.Recent.Count);
        Assert.Equal(8, data.Recent[0].Date.Day);
        Assert.Equal(3, data.Recent[^1].Date.Day);
    }

    [Fact]
    public async Task HasData_is_false_for_a_new_user()
    {
        Assert.False((await _dashboard.GetAsync("maria", new DateOnly(2025, 3, 1))).HasData);

        Add(EntryType.Expense, "2025-01-10", 1m);
        Assert.True((await _dashboard.GetAsync("maria", new DateOnly(2025, 3, 1))).HasData);
    }
}
