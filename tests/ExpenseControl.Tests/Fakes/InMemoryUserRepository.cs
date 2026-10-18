using ExpenseControl.Core.Abstractions;
using ExpenseControl.Core.Domain;

namespace ExpenseControl.Tests.Fakes;

internal sealed class InMemoryUserRepository : IUserRepository
{
    private sealed class Row
    {
        public required int Id { get; init; }
        public required string Username { get; init; }
        public required string Email { get; init; }
        public required string PasswordHash { get; set; }
        public string? TokenHash { get; set; }
        public DateTime? TokenExpiration { get; set; }
    }

    private readonly List<Row> _rows = [];

    public int PasswordHashUpdates { get; private set; }

    public void Add(string username, string email, string passwordHash) =>
        _rows.Add(new Row { Id = _rows.Count + 1, Username = username, Email = email, PasswordHash = passwordHash });

    public string? PasswordHashOf(string username) =>
        _rows.SingleOrDefault(r => Same(r.Username, username))?.PasswordHash;

    public string? TokenHashOf(string username) =>
        _rows.SingleOrDefault(r => Same(r.Username, username))?.TokenHash;

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.Where(r => Same(r.Username, username)).Select(ToUser).SingleOrDefault());

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.Where(r => Same(r.Email, email)).Select(ToUser).SingleOrDefault());

    public Task CreateAsync(NewUser user, CancellationToken cancellationToken = default)
    {
        if (_rows.Any(r => Same(r.Username, user.Username) || Same(r.Email, user.Email)))
        {
            throw new DuplicateUserException();
        }

        Add(user.Username, user.Email, user.PasswordHash);
        return Task.CompletedTask;
    }

    public Task UpdatePasswordHashAsync(int userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        _rows.Single(r => r.Id == userId).PasswordHash = passwordHash;
        PasswordHashUpdates++;
        return Task.CompletedTask;
    }

    public Task SetPasswordResetTokenAsync(int userId, string tokenHash, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        var row = _rows.Single(r => r.Id == userId);
        row.TokenHash = tokenHash;
        row.TokenExpiration = expiresAtUtc;
        return Task.CompletedTask;
    }

    public Task<bool> IsPasswordResetTokenValidAsync(string tokenHash, DateTime nowUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.Any(r => r.TokenHash == tokenHash && r.TokenExpiration > nowUtc));

    public Task<bool> ResetPasswordAsync(string tokenHash, string newPasswordHash, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var row = _rows.SingleOrDefault(r => r.TokenHash == tokenHash && r.TokenExpiration > nowUtc);
        if (row is null)
        {
            return Task.FromResult(false);
        }

        row.PasswordHash = newPasswordHash;
        row.TokenHash = null;
        row.TokenExpiration = null;
        return Task.FromResult(true);
    }

    // MySQL's default collation is case-insensitive, so the fake behaves the same way.
    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static User ToUser(Row row) => new(row.Id, row.Username, row.Email, row.PasswordHash);
}
