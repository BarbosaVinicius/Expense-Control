using System.Globalization;
using System.Text;
using ExpenseControl.Core.Domain;

namespace ExpenseControl.Core.Services;

/// <summary>Writes entries as CSV that opens correctly in the spreadsheet software of the given culture.</summary>
public static class LedgerCsv
{
    /// <param name="headers">Column titles: date, type, category, name, description, value.</param>
    /// <param name="typeLabel">Translated name of an entry type.</param>
    /// <param name="categoryLabel">Translated name of a category.</param>
    public static string Build(
        IEnumerable<LedgerEntry> entries,
        CultureInfo culture,
        IReadOnlyList<string> headers,
        Func<EntryType, string> typeLabel,
        Func<EntryType, string, string> categoryLabel)
    {
        // Spreadsheets use the culture's list separator (";" where the decimal separator is ","), so follow it.
        var separator = culture.TextInfo.ListSeparator;
        var builder = new StringBuilder();

        builder.AppendJoin(separator, headers.Select(h => Escape(h, separator))).Append("\r\n");

        foreach (var entry in entries)
        {
            var signedValue = entry.Type == EntryType.Expense ? -entry.Value : entry.Value;
            builder.AppendJoin(separator, new[]
            {
                Escape(entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), separator),
                Escape(typeLabel(entry.Type), separator),
                Escape(categoryLabel(entry.Type, entry.Category), separator),
                Escape(entry.Name, separator),
                Escape(entry.Description, separator),
                Escape(signedValue.ToString("0.00", culture), separator)
            }).Append("\r\n");
        }

        return builder.ToString();
    }

    private static string Escape(string text, string separator)
    {
        // Cells that start with these characters are executed as formulas by spreadsheet programs;
        // a leading apostrophe turns them back into plain text.
        if (text.Length > 0 && text[0] is '=' or '+' or '@' or '\t' or '\r' || (text.Length > 1 && text[0] == '-' && !char.IsDigit(text[1])))
        {
            text = "'" + text;
        }

        var needsQuotes = text.Contains(separator, StringComparison.Ordinal) ||
                          text.AsSpan().IndexOfAny('"', '\r', '\n') >= 0;
        return needsQuotes ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}
