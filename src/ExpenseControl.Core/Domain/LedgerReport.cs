namespace ExpenseControl.Core.Domain;

/// <summary>The entries of a user between two dates (inclusive) plus the derived totals.</summary>
public sealed record LedgerReport(DateOnly Start, DateOnly End, IReadOnlyList<LedgerEntry> Entries)
{
    public decimal TotalIncome => Entries.Where(e => e.Type == EntryType.Income).Sum(e => e.Value);

    public decimal TotalExpenses => Entries.Where(e => e.Type == EntryType.Expense).Sum(e => e.Value);

    public decimal Balance => TotalIncome - TotalExpenses;

    public int Count => Entries.Count;

    public bool IsEmpty => Entries.Count == 0;

    /// <summary>The same period, keeping only the entries accepted by the filter.</summary>
    public LedgerReport Filtered(LedgerFilter filter) =>
        filter.IsEmpty ? this : this with { Entries = filter.Apply(Entries).ToList() };
}
