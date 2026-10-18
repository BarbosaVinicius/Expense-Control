using ExpenseControl.Core.Domain;

namespace ExpenseControl.Tests;

public class CategoriesTests
{
    [Theory]
    [InlineData(EntryType.Expense)]
    [InlineData(EntryType.Income)]
    public void Every_type_has_unique_keys_and_ends_with_other(EntryType type)
    {
        var categories = Categories.For(type);

        Assert.Equal(categories.Count, categories.Select(c => c.Key).Distinct().Count());
        Assert.Equal(Categories.Other, categories[^1].Key);
        Assert.All(categories, c => Assert.Equal(type, c.Type));
    }

    [Fact]
    public void Expense_categories_fit_the_eight_colours_of_the_chart_palette()
    {
        Assert.True(Categories.Expense.Count <= 8);
        Assert.True(Categories.Income.Count <= 8);
    }

    [Fact]
    public void Validity_depends_on_the_entry_type()
    {
        Assert.True(Categories.IsValid(EntryType.Expense, "food"));
        Assert.False(Categories.IsValid(EntryType.Income, "food"));
        Assert.True(Categories.IsValid(EntryType.Income, "other"));
        Assert.False(Categories.IsValid(EntryType.Expense, null));
    }

    [Fact]
    public void Get_falls_back_to_other_for_unknown_keys()
    {
        Assert.Equal("other", Categories.Get(EntryType.Expense, "removed-long-ago").Key);
        Assert.Equal("food", Categories.Get(EntryType.Expense, "food").Key);
    }
}
