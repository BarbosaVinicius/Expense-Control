namespace ExpenseControl.Core.Domain;

/// <summary>A registered user as stored in the database.</summary>
public sealed record User(int Id, string Username, string Email, string PasswordHash);

/// <summary>Data required to create a new user. The password must already be hashed.</summary>
public sealed record NewUser(string Username, string Email, string PasswordHash);

/// <summary>The identity of a user who has successfully signed in.</summary>
public sealed record AuthenticatedUser(string Username);
