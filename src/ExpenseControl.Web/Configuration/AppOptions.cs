namespace ExpenseControl.Web.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Public address of the site (e.g. https://expenses.example.com), used to build links sent by e-mail.
    /// When empty, the address of the current request is used, which is fine locally but should be set in production.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Currency symbol shown before amounts. The app does not convert currencies; it only labels the numbers.</summary>
    public string CurrencySymbol { get; set; } = "R$";
}
