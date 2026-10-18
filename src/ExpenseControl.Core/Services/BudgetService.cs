using ExpenseControl.Core.Abstractions;
using ExpenseControl.Core.Common;
using ExpenseControl.Core.Domain;

namespace ExpenseControl.Core.Services;

/// <summary>Monthly spending limits per expense category.</summary>
public sealed class BudgetService(IBudgetRepository repository)
{
    public Task<IReadOnlyList<Budget>> GetBudgetsAsync(string username, CancellationToken cancellationToken = default) =>
        repository.GetAsync(username, cancellationToken);

    /// <summary>Sets the limit for a category. An amount of zero removes the budget.</summary>
    public async Task<OperationResult> SetBudgetAsync(
        string username, string category, decimal amount, CancellationToken cancellationToken = default)
    {
        if (!Categories.IsValid(EntryType.Expense, category))
        {
            return OperationResult.Failure("error.invalidCategory");
        }

        if (amount < 0 || amount > (decimal)LedgerLimits.MaxValue)
        {
            return OperationResult.Failure("error.budgetRange", (decimal)LedgerLimits.MaxValue);
        }

        if (decimal.Round(amount, 2) != amount)
        {
            return OperationResult.Failure("error.valueDecimals");
        }

        if (amount == 0)
        {
            await repository.DeleteAsync(username, category, cancellationToken);
        }
        else
        {
            await repository.SetAsync(username, new Budget(category, amount), cancellationToken);
        }

        return OperationResult.Success();
    }
}
