using System.Security.Cryptography;
using System.Text;
using ExpenseControl.Core.Abstractions;
using ExpenseControl.Infrastructure.Security;

namespace ExpenseControl.Tests;

public class Pbkdf2PasswordHasherTests
{
    // Few iterations keep the tests fast; the algorithm is the same.
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 10);

    [Fact]
    public void Verify_accepts_the_password_that_was_hashed()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_rejects_a_different_password()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify("wrong", hash));
    }

    [Fact]
    public void Hash_is_salted_so_equal_passwords_produce_different_hashes()
    {
        Assert.NotEqual(_hasher.Hash("same password"), _hasher.Hash("same password"));
    }

    [Fact]
    public void Hash_never_contains_the_password()
    {
        Assert.DoesNotContain("hunter2hunter2", _hasher.Hash("hunter2hunter2"));
    }

    [Fact]
    public void Verify_asks_for_a_rehash_when_the_stored_hash_used_fewer_iterations()
    {
        var oldHash = new Pbkdf2PasswordHasher(iterations: 5).Hash("secret123");

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, _hasher.Verify("secret123", oldHash));
    }

    [Fact]
    public void Verify_still_accepts_hashes_created_by_the_original_application()
    {
        // The original application stored Base64(SHA256(password)) without a salt.
        var legacyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("old password")));

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, _hasher.Verify("old password", legacyHash));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify("other password", legacyHash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a hash")]
    [InlineData("PBKDF2$abc$def$ghi")]
    [InlineData("PBKDF2$10$$")]
    [InlineData("PBKDF2$10$AAAA")]
    public void Verify_fails_without_throwing_for_malformed_hashes(string storedHash)
    {
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify("whatever", storedHash));
    }
}
