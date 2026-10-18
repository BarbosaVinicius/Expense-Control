namespace ExpenseControl.Core.Domain;

/// <summary>A monthly spending limit for one expense category.</summary>
public sealed record Budget(string Category, decimal Amount);
