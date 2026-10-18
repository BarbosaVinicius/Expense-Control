namespace ExpenseControl.Core.Common;

/// <summary>
/// Outcome of an operation that can fail for reasons the user should be told about.
/// <see cref="Error"/> is a translation key (for example "error.invalidCredentials") and <see cref="ErrorArgs"/>
/// are the values for its placeholders; the UI layer turns them into text in the user's language.
/// </summary>
public class OperationResult
{
    protected OperationResult(bool succeeded, string? error, object[]? errorArgs)
    {
        Succeeded = succeeded;
        Error = error;
        ErrorArgs = errorArgs ?? [];
    }

    public bool Succeeded { get; }

    public string? Error { get; }

    public IReadOnlyList<object> ErrorArgs { get; }

    public static OperationResult Success() => new(true, null, null);

    public static OperationResult Failure(string errorKey, params object[] args) => new(false, errorKey, args);
}

/// <inheritdoc />
public sealed class OperationResult<T> : OperationResult
{
    private OperationResult(bool succeeded, T? value, string? error, object[]? errorArgs)
        : base(succeeded, error, errorArgs)
    {
        Value = value;
    }

    public T? Value { get; }

    public static OperationResult<T> Success(T value) => new(true, value, null, null);

    public static new OperationResult<T> Failure(string errorKey, params object[] args) =>
        new(false, default, errorKey, args);
}
