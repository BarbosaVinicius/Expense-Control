using ExpenseControl.Core.Domain;

namespace ExpenseControl.Core.Abstractions;

public interface IBudgetRepository
{
    Task<IReadOnlyList<Budget>> GetAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>Creates the budget for the category or replaces the existing one.</summary>
    Task SetAsync(string username, Budget budget, CancellationToken cancellationToken = default);

    Task DeleteAsync(string username, string category, CancellationToken cancellationToken = default);
}
