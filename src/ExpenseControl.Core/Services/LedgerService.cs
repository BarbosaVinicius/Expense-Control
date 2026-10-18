using ExpenseControl.Core.Abstractions;
using ExpenseControl.Core.Common;
using ExpenseControl.Core.Domain;

namespace ExpenseControl.Core.Services;

/// <summary>Records, edits and removes expenses and amounts received, and builds reports from them.</summary>
public sealed class LedgerService(ILedgerRepository repository)
{
    /// <param name="category">One of <see cref="Categories"/> for the entry type; defaults to "other".</param>
    public async Task<OperationResult<LedgerEntry>> AddEntryAsync(
        EntryType type,
        string username,
        string? name,
        DateOnly date,
        decimal value,
        string? description,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var checkedEntry = Validate(new LedgerEntry(type, username, name ?? string.Empty, date, value, description ?? string.Empty, category ?? Categories.Other));
        if (checkedEntry.Error is not null)
        {
            return OperationResult<LedgerEntry>.Failure(checkedEntry.Error, checkedEntry.Args);
        }

        var entry = checkedEntry.Entry!;
        var id = await repository.AddAsync(entry, cancellationToken);
        return OperationResult<LedgerEntry>.Success(entry with { Id = id });
    }

    /// <summary>Replaces the editable fields of an existing entry (identified by <c>Type</c>, <c>Id</c> and <c>Username</c>).</summary>
    public async Task<OperationResult> UpdateEntryAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        var checkedEntry = Validate(entry);
        if (checkedEntry.Error is not null)
        {
            return OperationResult.Failure(checkedEntry.Error, checkedEntry.Args);
        }

        var updated = await repository.UpdateAsync(checkedEntry.Entry!, cancellationToken);
        return updated ? OperationResult.Success() : OperationResult.Failure("error.entryNotFound");
    }

    public async Task<OperationResult> DeleteEntryAsync(
        string username, EntryType type, int id, CancellationToken cancellationToken = default)
    {
        var deleted = await repository.DeleteAsync(username, type, id, cancellationToken);
        return deleted ? OperationResult.Success() : OperationResult.Failure("error.entryNotFound");
    }

    public Task<LedgerEntry?> GetEntryAsync(
        string username, EntryType type, int id, CancellationToken cancellationToken = default) =>
        repository.GetByIdAsync(username, type, id, cancellationToken);

    public async Task<OperationResult<LedgerReport>> GetReportAsync(
        string username, DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        if (start > end)
        {
            return OperationResult<LedgerReport>.Failure("error.dateRange");
        }

        var entries = await repository.GetAsync(username, start, end, cancellationToken);
        return OperationResult<LedgerReport>.Success(new LedgerReport(start, end, entries));
    }

    private static (LedgerEntry? Entry, string? Error, object[] Args) Validate(LedgerEntry entry)
    {
        var name = entry.Name.Trim();
        var description = entry.Description.Trim();

        if (name.Length == 0 || name.Length > LedgerLimits.MaxNameLength)
        {
            return (null, "error.nameRequired", [LedgerLimits.MaxNameLength]);
        }

        // The description is optional.
        if (description.Length > LedgerLimits.MaxDescriptionLength)
        {
            return (null, "error.descriptionTooLong", [LedgerLimits.MaxDescriptionLength]);
        }

        if (entry.Value <= 0 || entry.Value > (decimal)LedgerLimits.MaxValue)
        {
            return (null, "error.valueRange", [(decimal)LedgerLimits.MaxValue]);
        }

        if (decimal.Round(entry.Value, 2) != entry.Value)
        {
            return (null, "error.valueDecimals", []);
        }

        if (!Categories.IsValid(entry.Type, entry.Category))
        {
            return (null, "error.invalidCategory", []);
        }

        return (entry with { Name = name, Description = description }, null, []);
    }
}
