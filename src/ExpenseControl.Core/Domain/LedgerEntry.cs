namespace ExpenseControl.Core.Domain;

public enum EntryType
{
    Expense,
    Income
}

/// <summary>A single money movement (an expense or an amount received) belonging to a user.</summary>
/// <param name="Id">Database id, unique within one <see cref="EntryType"/>; 0 for an entry that was not saved yet.</param>
public sealed record LedgerEntry(
    EntryType Type,
    string Username,
    string Name,
    DateOnly Date,
    decimal Value,
    string Description,
    string Category = Categories.Other,
    int Id = 0);

/// <summary>Limits shared by validation (services), persistence (column sizes) and the UI (input lengths).</summary>
public static class LedgerLimits
{
    public const int MaxNameLength = 25;
    public const int MaxDescriptionLength = 75;

    /// <summary>Largest value that fits the DECIMAL(10, 2) column.</summary>
    public const double MaxValue = 99_999_999.99;
}

/// <summary>Narrows a list of entries. All parts are optional and combine with AND.</summary>
public sealed record LedgerFilter(EntryType? Type = null, string? Category = null, string? Text = null)
{
    public static LedgerFilter None { get; } = new();

    public bool IsEmpty => Type is null && string.IsNullOrEmpty(Category) && string.IsNullOrWhiteSpace(Text);

    public IEnumerable<LedgerEntry> Apply(IEnumerable<LedgerEntry> entries)
    {
        var text = Text?.Trim();
        return entries.Where(e =>
            (Type is null || e.Type == Type) &&
            (string.IsNullOrEmpty(Category) || e.Category == Category) &&
            (string.IsNullOrEmpty(text) ||
             e.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase) ||
             e.Description.Contains(text, StringComparison.CurrentCultureIgnoreCase)));
    }
}
