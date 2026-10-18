using ExpenseControl.Core.Abstractions;
using ExpenseControl.Core.Domain;

namespace ExpenseControl.Tests.Fakes;

internal sealed class InMemoryLedgerRepository : ILedgerRepository
{
    private int _nextId = 1;

    public List<LedgerEntry> Entries { get; } = [];

    public Task<int> AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        var id = _nextId++;
        Entries.Add(entry with { Id = id });
        return Task.FromResult(id);
    }

    public Task<LedgerEntry?> GetByIdAsync(string username, EntryType type, int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Entries.FirstOrDefault(e => e.Username == username && e.Type == type && e.Id == id));

    public Task<bool> UpdateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        var index = Entries.FindIndex(e => e.Username == entry.Username && e.Type == entry.Type && e.Id == entry.Id);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        Entries[index] = entry;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string username, EntryType type, int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Entries.RemoveAll(e => e.Username == username && e.Type == type && e.Id == id) > 0);

    public Task<IReadOnlyList<LedgerEntry>> GetAsync(
        string username, DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LedgerEntry> result = Entries
            .Where(e => e.Username == username && e.Date >= start && e.Date <= end)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Id)
            .ToList();
        return Task.FromResult(result);
    }
}
