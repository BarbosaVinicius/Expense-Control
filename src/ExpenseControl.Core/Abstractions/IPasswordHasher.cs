namespace ExpenseControl.Core.Abstractions;

public enum PasswordVerificationResult
{
    Failed,
    Success,

    /// <summary>The password is correct but the stored hash uses an outdated format and should be replaced.</summary>
    SuccessRehashNeeded
}

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerificationResult Verify(string password, string storedHash);
}
