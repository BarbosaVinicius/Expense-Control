using ExpenseControl.Web.Configuration;
using ExpenseControl.Web.Localization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace ExpenseControl.Tests.Web;

public class FormatterTests
{
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);
    }

    private static async Task<Formatter> FormatterFor(string language, string symbol = "R$")
    {
        var state = new LanguageState(new NoJs());
        await state.SetAsync(language);
        return new Formatter(state, Options.Create(new AppOptions { CurrencySymbol = symbol }));
    }

    [Theory]
    [InlineData("pt")]
    [InlineData("en")]
    [InlineData("es")]
    public async Task The_currency_symbol_does_not_change_with_the_language(string language)
    {
        var text = (await FormatterFor(language)).Money(1234.5m);

        Assert.Contains("R$", text);
        Assert.DoesNotContain("€", text);
        Assert.Contains("1", text);
    }

    [Fact]
    public async Task Numbers_follow_the_conventions_of_each_language()
    {
        Assert.Contains("1.234,50", (await FormatterFor("pt")).Money(1234.5m));
        Assert.Contains("1,234.50", (await FormatterFor("en")).Money(1234.5m));
        Assert.Contains("1.234,50", (await FormatterFor("es")).Money(1234.5m));
    }

    [Fact]
    public async Task The_symbol_is_configurable()
    {
        Assert.Contains("US$", (await FormatterFor("en", "US$")).Money(5m));
    }

    [Fact]
    public async Task Negative_amounts_keep_their_sign()
    {
        Assert.Contains("-", (await FormatterFor("pt")).Money(-10m));
    }

    [Theory]
    [InlineData("en", "March 2025")]
    [InlineData("pt", "Março de 2025")]
    [InlineData("es", "Marzo de 2025")]
    public async Task Month_and_year_are_written_in_the_current_language(string language, string expected)
    {
        Assert.Equal(expected, (await FormatterFor(language)).MonthYear(new DateOnly(2025, 3, 1)));
    }

    [Fact]
    public async Task Dates_percentages_and_deltas_are_culture_aware()
    {
        var pt = await FormatterFor("pt");
        var en = await FormatterFor("en");

        Assert.Equal("05/03/2025", pt.Date(new DateOnly(2025, 3, 5)));
        Assert.Equal("3/5/2025", en.Date(new DateOnly(2025, 3, 5)));
        Assert.Contains("12", pt.Percent(0.12m));
        Assert.Equal("+12%", en.SignedPercent(0.12m));
        Assert.Equal("-5%", en.SignedPercent(-0.05m));
    }

    [Fact]
    public async Task The_language_state_reports_what_was_selected()
    {
        var state = new LanguageState(new NoJs());
        var changes = 0;
        state.Changed += () => changes++;

        await state.SetAsync("es");
        await state.SetAsync("not-a-language");

        Assert.Equal(2, changes);
        Assert.Equal("en", state.Code);   // unknown codes fall back to the default
    }
}
