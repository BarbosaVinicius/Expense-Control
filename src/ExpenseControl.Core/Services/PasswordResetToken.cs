using System.Security.Cryptography;
using System.Text;

namespace ExpenseControl.Core.Services;

/// <summary>
/// Reset tokens are random secrets sent by e-mail. Only their SHA-256 hash is stored, so a database leak
/// does not allow anyone to reset a password.
/// </summary>
internal static class PasswordResetToken
{
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
