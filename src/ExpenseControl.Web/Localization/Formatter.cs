using System.Globalization;
using ExpenseControl.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ExpenseControl.Web.Localization;

/// <summary>
/// Formats money, dates and percentages for the current language. The currency symbol comes from configuration
/// (<c>App:CurrencySymbol</c>) and does not change with the language: switching to Spanish must not turn R$ into euros.
/// </summary>
public sealed class Formatter(LanguageState language, IOptions<AppOptions> options)
{
    public CultureInfo Culture => language.Culture;

    public string CurrencySymbol => options.Value.CurrencySymbol;

    public string Money(decimal value)
    {
        var format = (NumberFormatInfo)Culture.NumberFormat.Clone();
        format.CurrencySymbol = CurrencySymbol;
        return value.ToString("C2", format);
    }

    public string Date(DateOnly date) => date.ToString("d", Culture);

    /// <summary>"March 2025", "março de 2025", "marzo de 2025".</summary>
    public string MonthYear(DateOnly date) => Capitalize(date.ToString(Culture.DateTimeFormat.YearMonthPattern, Culture));

    public string ShortMonth(DateOnly date) =>
        Capitalize(Culture.DateTimeFormat.GetAbbreviatedMonthName(date.Month).TrimEnd('.'));

    public string Percent(decimal ratio) => ratio.ToString("P0", Culture);

    /// <summary>"+12%" / "-5%" for deltas.</summary>
    public string SignedPercent(decimal ratio) => ratio.ToString("+0%;-0%;0%", Culture);

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
