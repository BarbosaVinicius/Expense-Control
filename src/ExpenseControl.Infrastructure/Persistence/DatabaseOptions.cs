namespace ExpenseControl.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>When true (the default), pending schema migrations are applied when the application starts.</summary>
    public bool MigrateOnStartup { get; set; } = true;
}
