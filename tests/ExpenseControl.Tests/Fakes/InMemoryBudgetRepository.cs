using ExpenseControl.Core.Abstractions;
using ExpenseControl.Core.Domain;

namespace ExpenseControl.Tests.Fakes;

internal sealed class InMemoryBudgetRepository : IBudgetRepository
{
    private readonly Dictionary<(string User, string Category), decimal> _budgets = [];

    public Task<IReadOnlyList<Budget>> GetAsync(string username, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Budget> result = _budgets
            .Where(b => b.Key.User == username)
            .Select(b => new Budget(b.Key.Category, b.Value))
            .OrderBy(b => b.Category)
            .ToList();
        return Task.FromResult(result);
    }

    public Task SetAsync(string username, Budget budget, CancellationToken cancellationToken = default)
    {
        _budgets[(username, budget.Category)] = budget.Amount;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string username, string category, CancellationToken cancellationToken = default)
    {
        _budgets.Remove((username, category));
        return Task.CompletedTask;
    }
}
