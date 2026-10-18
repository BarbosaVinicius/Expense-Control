using ExpenseControl.Core.Domain;
using ExpenseControl.Core.Services;
using ExpenseControl.Tests.Fakes;

namespace ExpenseControl.Tests;

public class LedgerServiceTests
{
    private static readonly DateOnly Today = new(2025, 3, 15);

    private readonly InMemoryLedgerRepository _repository = new();
    private readonly LedgerService _ledger;

    public LedgerServiceTests()
    {
        _ledger = new LedgerService(_repository);
    }

    [Fact]
    public async Task AddEntry_stores_a_valid_entry_with_trimmed_text_and_returns_it_with_its_id()
    {
        var result = await _ledger.AddEntryAsync(EntryType.Expense, "maria", "  Groceries ", Today, 42.50m, " Weekly shopping ", "food");

        Assert.True(result.Succeeded);
        var expected = new LedgerEntry(EntryType.Expense, "maria", "Groceries", Today, 42.50m, "Weekly shopping", "food", 1);
        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, Assert.Single(_repository.Entries));
    }

    [Fact]
    public async Task AddEntry_defaults_to_the_other_category_and_accepts_an_empty_description()
    {
        var result = await _ledger.AddEntryAsync(EntryType.Income, "maria", "Gift", Today, 10m, null);

        Assert.True(result.Succeeded);
        Assert.Equal(Categories.Other, result.Value!.Category);
        Assert.Equal(string.Empty, result.Value.Description);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("   ", 10)]
    [InlineData(null, 10)]
    [InlineData("A name that is definitely longer than 25 characters", 10)]
    [InlineData("name", 0)]
    [InlineData("name", -5)]
    [InlineData("name", 1.005)]          // more than two decimal places
    [InlineData("name", 100000000)]      // does not fit DECIMAL(10, 2)
    public async Task AddEntry_rejects_invalid_input(string? name, double value)
    {
        var result = await _ledger.AddEntryAsync(EntryType.Expense, "maria", name, Today, (decimal)value, "desc");

        Assert.False(result.Succeeded);
        Assert.StartsWith("error.", result.Error);
        Assert.Empty(_repository.Entries);
    }

    [Fact]
    public async Task AddEntry_rejects_a_description_longer_than_the_column()
    {
        var description = new string('x', LedgerLimits.MaxDescriptionLength + 1);

        var result = await _ledger.AddEntryAsync(EntryType.Income, "maria", "Salary", Today, 10m, description);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(EntryType.Expense, "salary")]   // an income category on an expense
    [InlineData(EntryType.Income, "housing")]   // an expense category on an income
    [InlineData(EntryType.Expense, "nonsense")]
    public async Task AddEntry_rejects_a_category_that_does_not_belong_to_the_entry_type(EntryType type, string category)
    {
        var result = await _ledger.AddEntryAsync(type, "maria", "Name", Today, 10m, "d", category);

        Assert.False(result.Succeeded);
        Assert.Equal("error.invalidCategory", result.Error);
    }

    [Fact]
    public async Task UpdateEntry_changes_the_editable_fields()
    {
        var added = (await _ledger.AddEntryAsync(EntryType.Expense, "maria", "Rent", Today, 1000m, "old", "housing")).Value!;

        var result = await _ledger.UpdateEntryAsync(added with { Name = "Rent!", Value = 1200m, Description = "new", Category = "other" });

        Assert.True(result.Succeeded);
        var stored = Assert.Single(_repository.Entries);
        Assert.Equal(("Rent!", 1200m, "new", "other"), (stored.Name, stored.Value, stored.Description, stored.Category));
    }

    [Fact]
    public async Task UpdateEntry_validates_like_add_and_fails_for_unknown_or_foreign_entries()
    {
        var added = (await _ledger.AddEntryAsync(EntryType.Expense, "maria", "Rent", Today, 1000m, "x")).Value!;

        Assert.False((await _ledger.UpdateEntryAsync(added with { Value = -1m })).Succeeded);
        Assert.Equal("error.entryNotFound", (await _ledger.UpdateEntryAsync(added with { Id = 99 })).Error);
        Assert.Equal("error.entryNotFound", (await _ledger.UpdateEntryAsync(added with { Username = "joao" })).Error);
    }

    [Fact]
    public async Task DeleteEntry_removes_only_the_users_own_entry()
    {
        var added = (await _ledger.AddEntryAsync(EntryType.Expense, "maria", "Rent", Today, 1000m, "x")).Value!;

        Assert.False((await _ledger.DeleteEntryAsync("joao", EntryType.Expense, added.Id)).Succeeded);
        Assert.Single(_repository.Entries);

        Assert.True((await _ledger.DeleteEntryAsync("maria", EntryType.Expense, added.Id)).Succeeded);
        Assert.Empty(_repository.Entries);
        Assert.False((await _ledger.DeleteEntryAsync("maria", EntryType.Expense, added.Id)).Succeeded);
    }

    [Fact]
    public async Task GetReport_returns_only_the_users_entries_in_the_period_with_totals()
    {
        await _ledger.AddEntryAsync(EntryType.Income, "maria", "Salary", new DateOnly(2025, 3, 1), 3000m, "March salary");
        await _ledger.AddEntryAsync(EntryType.Expense, "maria", "Rent", new DateOnly(2025, 3, 5), 1200m, "March rent");
        await _ledger.AddEntryAsync(EntryType.Expense, "maria", "Food", new DateOnly(2025, 3, 10), 300.25m, "Groceries");
        await _ledger.AddEntryAsync(EntryType.Expense, "maria", "Old", new DateOnly(2025, 2, 28), 999m, "Outside the period");
        await _ledger.AddEntryAsync(EntryType.Income, "joao", "Salary", new DateOnly(2025, 3, 1), 5000m, "Someone else");

        var result = await _ledger.GetReportAsync("maria", new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));

        Assert.True(result.Succeeded);
        var report = result.Value!;
        Assert.Equal(3, report.Entries.Count);
        Assert.Equal(3000m, report.TotalIncome);
        Assert.Equal(1500.25m, report.TotalExpenses);
        Assert.Equal(1499.75m, report.Balance);
    }

    [Fact]
    public async Task GetReport_includes_both_boundary_dates()
    {
        await _ledger.AddEntryAsync(EntryType.Expense, "maria", "First", new DateOnly(2025, 3, 1), 1m, "first day");
        await _ledger.AddEntryAsync(EntryType.Expense, "maria", "Last", new DateOnly(2025, 3, 31), 1m, "last day");

        var result = await _ledger.GetReportAsync("maria", new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));

        Assert.Equal(2, result.Value!.Entries.Count);
    }

    [Fact]
    public async Task GetReport_is_empty_when_there_is_nothing_in_the_period()
    {
        var result = await _ledger.GetReportAsync("maria", Today, Today);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.IsEmpty);
        Assert.Equal(0m, result.Value.Balance);
    }

    [Fact]
    public async Task GetReport_rejects_a_start_date_after_the_end_date()
    {
        var result = await _ledger.GetReportAsync("maria", Today.AddDays(1), Today);

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Filters_combine_type_category_and_text_and_recompute_the_totals()
    {
        await _ledger.AddEntryAsync(EntryType.Income, "maria", "Salary", Today, 3000m, "March", "salary");
        await _ledger.AddEntryAsync(EntryType.Expense, "maria", "Supermarket", Today, 200m, "Weekly shop", "food");
        await _ledger.AddEntryAsync(EntryType.Expense, "maria", "Restaurant", Today, 80m, "Dinner", "food");
        await _ledger.AddEntryAsync(EntryType.Expense, "maria", "Bus", Today, 20m, "Tickets", "transport");
        var report = (await _ledger.GetReportAsync("maria", Today, Today)).Value!;

        Assert.Equal(4, report.Filtered(LedgerFilter.None).Entries.Count);
        Assert.Equal(3, report.Filtered(new LedgerFilter(Type: EntryType.Expense)).Entries.Count);
        Assert.Equal(280m, report.Filtered(new LedgerFilter(Category: "food")).TotalExpenses);
        Assert.Single(report.Filtered(new LedgerFilter(Text: "DINNER")).Entries);              // searches the description, ignoring case
        Assert.Single(report.Filtered(new LedgerFilter(Text: "super", Category: "food")).Entries);
        Assert.Empty(report.Filtered(new LedgerFilter(Type: EntryType.Income, Category: "food")).Entries);
    }
}
