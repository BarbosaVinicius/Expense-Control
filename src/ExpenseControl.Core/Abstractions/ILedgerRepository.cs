using ExpenseControl.Core.Domain;

namespace ExpenseControl.Core.Abstractions;

public interface ILedgerRepository
{
    /// <summary>Saves the entry and returns its new id.</summary>
    Task<int> AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default);

    Task<LedgerEntry?> GetByIdAsync(string username, EntryType type, int id, CancellationToken cancellationToken = default);

    /// <summary>Overwrites name, date, value, description and category of an existing entry. Returns false if it does not exist.</summary>
    Task<bool> UpdateAsync(LedgerEntry entry, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string username, EntryType type, int id, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's entries (expenses and income) between the dates, inclusive, oldest first.</summary>
    Task<IReadOnlyList<LedgerEntry>> GetAsync(string username, DateOnly start, DateOnly end, CancellationToken cancellationToken = default);
}
