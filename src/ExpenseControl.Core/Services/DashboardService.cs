using ExpenseControl.Core.Abstractions;
using ExpenseControl.Core.Domain;

namespace ExpenseControl.Core.Services;

/// <summary>Builds the figures shown on the dashboard from the user's entries and budgets.</summary>
public sealed class DashboardService(ILedgerRepository ledger, IBudgetRepository budgets, TimeProvider clock)
{
    /// <summary>Number of months (ending at the selected one) in the income-vs-expenses trend.</summary>
    public const int TrendMonths = 6;

    private const int RecentCount = 6;

    /// <param name="anyDayInMonth">Any date inside the month to show.</param>
    public async Task<DashboardData> GetAsync(
        string username, DateOnly anyDayInMonth, CancellationToken cancellationToken = default)
    {
        var month = new DateOnly(anyDayInMonth.Year, anyDayInMonth.Month, 1);
        var firstTrendMonth = month.AddMonths(-(TrendMonths - 1));
        var lastDay = month.AddMonths(1).AddDays(-1);

        // One query for the whole window; everything else is computed here.
        var entries = await ledger.GetAsync(username, firstTrendMonth, lastDay, cancellationToken);
        var userBudgets = await budgets.GetAsync(username, cancellationToken);

        var trend = Enumerable.Range(0, TrendMonths)
            .Select(i => Summarize(entries, firstTrendMonth.AddMonths(i)))
            .ToList();
        var current = trend[^1];
        var previous = trend[^2];

        var monthExpenses = entries
            .Where(e => e.Type == EntryType.Expense && InMonth(e.Date, month))
            .ToList();

        return new DashboardData(
            month,
            current,
            previous,
            trend,
            ByCategory(monthExpenses),
            Cumulative(monthExpenses, month),
            BudgetStatuses(userBudgets, monthExpenses),
            entries
                .Where(e => InMonth(e.Date, month))
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.Id)
                .Take(RecentCount)
                .ToList());
    }

    private static MonthSummary Summarize(IEnumerable<LedgerEntry> entries, DateOnly month)
    {
        var inMonth = entries.Where(e => InMonth(e.Date, month)).ToList();
        return new MonthSummary(
            month,
            inMonth.Where(e => e.Type == EntryType.Income).Sum(e => e.Value),
            inMonth.Where(e => e.Type == EntryType.Expense).Sum(e => e.Value));
    }

    private static List<CategoryTotal> ByCategory(List<LedgerEntry> monthExpenses)
    {
        var total = monthExpenses.Sum(e => e.Value);
        return monthExpenses
            .GroupBy(e => e.Category)
            .Select(g => new CategoryTotal(g.Key, g.Sum(e => e.Value), total > 0 ? g.Sum(e => e.Value) / total : 0))
            .OrderByDescending(c => c.Total)
            .ToList();
    }

    private List<DailyPoint> Cumulative(List<LedgerEntry> monthExpenses, DateOnly month)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);

        // For the running month stop at today, so the line does not flat-line into the future.
        var lastDayShown = month > today ? 0 : InMonth(today, month) ? today.Day : daysInMonth;

        var perDay = monthExpenses.GroupBy(e => e.Date.Day).ToDictionary(g => g.Key, g => g.Sum(e => e.Value));
        var points = new List<DailyPoint>(lastDayShown);
        var running = 0m;
        for (var day = 1; day <= lastDayShown; day++)
        {
            running += perDay.GetValueOrDefault(day);
            points.Add(new DailyPoint(day, running));
        }

        return points;
    }

    private static List<BudgetStatus> BudgetStatuses(IReadOnlyList<Budget> userBudgets, List<LedgerEntry> monthExpenses)
    {
        var spent = monthExpenses.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Sum(e => e.Value));
        return userBudgets
            .Select(b => new BudgetStatus(b.Category, b.Amount, spent.GetValueOrDefault(b.Category)))
            .OrderByDescending(s => s.Ratio)
            .ToList();
    }

    private static bool InMonth(DateOnly date, DateOnly month) => date.Year == month.Year && date.Month == month.Month;
}
