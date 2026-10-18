namespace ExpenseControl.Core.Domain;

/// <summary>A category an entry can be filed under. The display name is looked up by key in the UI ("category.{key}").</summary>
/// <param name="Key">Stable identifier stored in the database.</param>
/// <param name="Icon">Bootstrap Icons class used by the UI.</param>
public sealed record Category(string Key, EntryType Type, string Icon);

/// <summary>
/// The fixed catalog of categories. Keys are stored in the database, so never rename one;
/// add new ones at the end (and add their translations).
/// </summary>
public static class Categories
{
    public const string Other = "other";

    public static IReadOnlyList<Category> Expense { get; } =
    [
        new("housing", EntryType.Expense, "bi-house"),
        new("food", EntryType.Expense, "bi-basket"),
        new("transport", EntryType.Expense, "bi-bus-front"),
        new("health", EntryType.Expense, "bi-heart-pulse"),
        new("leisure", EntryType.Expense, "bi-controller"),
        new("education", EntryType.Expense, "bi-mortarboard"),
        new("shopping", EntryType.Expense, "bi-bag"),
        new(Other, EntryType.Expense, "bi-three-dots")
    ];

    public static IReadOnlyList<Category> Income { get; } =
    [
        new("salary", EntryType.Income, "bi-briefcase"),
        new("freelance", EntryType.Income, "bi-laptop"),
        new("investments", EntryType.Income, "bi-graph-up-arrow"),
        new("gift", EntryType.Income, "bi-gift"),
        new(Other, EntryType.Income, "bi-three-dots")
    ];

    public static IReadOnlyList<Category> For(EntryType type) => type == EntryType.Expense ? Expense : Income;

    public static bool IsValid(EntryType type, string? key) =>
        key is not null && For(type).Any(c => c.Key == key);

    public static Category Get(EntryType type, string? key) =>
        For(type).FirstOrDefault(c => c.Key == key) ?? For(type)[^1];

    /// <summary>Every distinct key of both types, in a stable order (used by filters).</summary>
    public static IReadOnlyList<string> AllKeys { get; } =
        Expense.Concat(Income).Select(c => c.Key).Distinct().ToList();
}
