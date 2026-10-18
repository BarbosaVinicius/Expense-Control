using System.Globalization;
using ExpenseControl.Core.Domain;
using ExpenseControl.Core.Services;

namespace ExpenseControl.Tests;

public class LedgerCsvTests
{
    private static readonly string[] Headers = ["Date", "Type", "Category", "Name", "Description", "Value"];

    private static string Build(CultureInfo culture, params LedgerEntry[] entries) =>
        LedgerCsv.Build(entries, culture, Headers, t => t.ToString(), (_, c) => c.ToUpperInvariant());

    private static LedgerEntry Entry(EntryType type, string name, string description, decimal value) =>
        new(type, "maria", name, new DateOnly(2025, 3, 5), value, description, "food");

    [Fact]
    public void English_culture_uses_commas_and_dots_and_signs_expenses_negative()
    {
        var csv = Build(new CultureInfo("en-US"),
            Entry(EntryType.Expense, "Rent", "March", 1200.5m),
            Entry(EntryType.Income, "Salary", "March", 3000m));

        Assert.Equal(
            "Date,Type,Category,Name,Description,Value\r\n" +
            "2025-03-05,Expense,FOOD,Rent,March,-1200.50\r\n" +
            "2025-03-05,Income,FOOD,Salary,March,3000.00\r\n",
            csv);
    }

    [Fact]
    public void Brazilian_culture_uses_semicolons_and_decimal_commas_so_excel_opens_it_in_columns()
    {
        var csv = Build(new CultureInfo("pt-BR"), Entry(EntryType.Expense, "Aluguel", "Março", 1200.5m));

        Assert.Equal(
            "Date;Type;Category;Name;Description;Value\r\n" +
            "2025-03-05;Expense;FOOD;Aluguel;Março;-1200,50\r\n",
            csv);
    }

    [Fact]
    public void Text_with_separators_quotes_or_line_breaks_is_quoted()
    {
        var csv = Build(new CultureInfo("en-US"), Entry(EntryType.Expense, "Fish, chips", "He said \"yum\"\nthen left", 5m));

        Assert.Contains("\"Fish, chips\",\"He said \"\"yum\"\"\nthen left\"", csv);
    }

    [Theory]
    [InlineData("=SUM(A1:A9)")]
    [InlineData("+1+1")]
    [InlineData("@cmd")]
    [InlineData("-cmd")]
    public void Text_that_a_spreadsheet_would_run_as_a_formula_is_neutralised(string name)
    {
        var csv = Build(new CultureInfo("en-US"), Entry(EntryType.Expense, name, "x", 1m));

        Assert.Contains(",'" + name + ",", csv);
    }

    [Fact]
    public void No_entries_still_produce_the_header()
    {
        Assert.Equal("Date,Type,Category,Name,Description,Value\r\n", Build(new CultureInfo("en-US")));
    }
}
