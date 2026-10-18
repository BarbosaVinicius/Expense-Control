using System.Security.Cryptography;
using System.Text;
using ExpenseControl.Core.Abstractions;

namespace ExpenseControl.Infrastructure.Security;

/// <summary>
/// Salted PBKDF2-SHA256 password hashing. Hashes look like <c>PBKDF2$iterations$salt$key</c>, so the cost can be
/// raised later and old hashes are upgraded the next time their owner signs in.
/// </summary>
/// <remarks>
/// Accounts created by the original version of the application stored an unsalted, base64-encoded SHA-256 of the
/// password. Those hashes are still accepted, and reported as needing a rehash.
/// </remarks>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int DefaultIterations = 600_000;

    private const string Prefix = "PBKDF2";
    private const int SaltSize = 16;
    private const int KeySize = 32;

    private readonly int _iterations;

    public Pbkdf2PasswordHasher() : this(DefaultIterations)
    {
    }

    public Pbkdf2PasswordHasher(int iterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        _iterations = iterations;
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Derive(password, salt, _iterations);
        return $"{Prefix}${_iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public PasswordVerificationResult Verify(string password, string storedHash)
    {
        if (storedHash.StartsWith(Prefix + "$", StringComparison.Ordinal))
        {
            return VerifyPbkdf2(password, storedHash);
        }

        return VerifyLegacySha256(password, storedHash);
    }

    private PasswordVerificationResult VerifyPbkdf2(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations) || iterations < 1)
        {
            return PasswordVerificationResult.Failed;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length == 0 || expected.Length == 0)
            {
                return PasswordVerificationResult.Failed;
            }

            var actual = Derive(password, salt, iterations, expected.Length);

            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                return PasswordVerificationResult.Failed;
            }

            return iterations < _iterations
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Success;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            // Malformed stored hash (bad base64 or empty salt/key).
            return PasswordVerificationResult.Failed;
        }
    }

    private static PasswordVerificationResult VerifyLegacySha256(string password, string storedHash)
    {
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));

        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }

        return CryptographicOperations.FixedTimeEquals(actual, expected)
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Failed;
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int keySize = KeySize) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, keySize);
}
