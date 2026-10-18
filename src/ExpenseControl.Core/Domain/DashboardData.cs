namespace ExpenseControl.Core.Domain;

/// <summary>Totals for one calendar month.</summary>
public sealed record MonthSummary(DateOnly Month, decimal Income, decimal Expenses)
{
    public decimal Balance => Income - Expenses;
}

/// <summary>Spending in one category and its share (0 to 1) of the month's expenses.</summary>
public sealed record CategoryTotal(string Category, decimal Total, decimal Share);

/// <summary>Progress of one category against its monthly budget.</summary>
public sealed record BudgetStatus(string Category, decimal Limit, decimal Spent)
{
    /// <summary>Spent divided by the limit; 1 means the budget is used up.</summary>
    public decimal Ratio => Limit > 0 ? Spent / Limit : 0;

    public decimal Remaining => Limit - Spent;

    public bool IsOver => Spent > Limit;
}

/// <summary>Expenses accumulated up to and including <see cref="Day"/>.</summary>
public sealed record DailyPoint(int Day, decimal Cumulative);

/// <summary>Everything the dashboard shows for one month.</summary>
public sealed record DashboardData(
    DateOnly Month,
    MonthSummary Current,
    MonthSummary Previous,
    IReadOnlyList<MonthSummary> Trend,
    IReadOnlyList<CategoryTotal> ExpensesByCategory,
    IReadOnlyList<DailyPoint> CumulativeExpenses,
    IReadOnlyList<BudgetStatus> Budgets,
    IReadOnlyList<LedgerEntry> Recent)
{
    /// <summary>Share of the month's income that was not spent (0 to 1, negative when overspent); null without income.</summary>
    public decimal? SavingsRate => Current.Income > 0 ? Current.Balance / Current.Income : null;

    public bool HasData => Trend.Any(m => m.Income > 0 || m.Expenses > 0);

    /// <summary>Relative change from <paramref name="previous"/> to <paramref name="current"/> (0.1 = +10%); null when there is no base.</summary>
    public static decimal? PercentChange(decimal previous, decimal current) =>
        previous > 0 ? (current - previous) / previous : null;
}
