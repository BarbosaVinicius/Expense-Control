using System.Text.RegularExpressions;
using ExpenseControl.Core.Domain;
using ExpenseControl.Web.Localization;

namespace ExpenseControl.Tests.Web;

public partial class LocalizationTests
{
    private static readonly TranslationStore Store = new();

    [Fact]
    public void Every_language_has_exactly_the_same_keys_as_english()
    {
        var english = Store.Keys("en").ToHashSet();

        foreach (var language in TranslationStore.Languages)
        {
            var keys = Store.Keys(language.Code).ToHashSet();
            Assert.Empty(english.Except(keys));   // missing translations
            Assert.Empty(keys.Except(english));   // keys that exist only in this language
        }
    }

    [Fact]
    public void No_translation_is_empty()
    {
        foreach (var language in TranslationStore.Languages)
        {
            foreach (var key in Store.Keys(language.Code))
            {
                Assert.False(string.IsNullOrWhiteSpace(Store.Get(language.Code, key)), $"{language.Code}: {key} is empty");
            }
        }
    }

    [Fact]
    public void Placeholders_match_across_languages()
    {
        // "{0} of {1}" in English must also have {0} and {1} in Portuguese and Spanish, or Format would throw or drop a value.
        foreach (var key in Store.Keys("en"))
        {
            var expected = Placeholders(Store.Get("en", key));
            foreach (var language in TranslationStore.Languages)
            {
                Assert.True(
                    expected.SetEquals(Placeholders(Store.Get(language.Code, key))),
                    $"{language.Code}: placeholders of '{key}' differ from English");
            }
        }
    }

    [Fact]
    public void Texts_can_be_formatted_with_their_placeholders_in_every_culture()
    {
        foreach (var language in TranslationStore.Languages)
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo(language.Culture);
            foreach (var key in Store.Keys(language.Code))
            {
                var text = Store.Get(language.Code, key);
                var arguments = Enumerable.Range(0, Placeholders(Store.Get("en", key)).Count).Select(_ => (object)1234.5m).ToArray();
                var ex = Record.Exception(() => string.Format(culture, text, arguments));
                Assert.Null(ex);
            }
        }
    }

    [Fact]
    public void Every_text_used_by_the_pages_exists()
    {
        var english = Store.Keys("en").ToHashSet();
        var used = LiteralKeysUsedBySources();

        Assert.NotEmpty(used);
        Assert.Empty(used.Except(english));
    }

    [Fact]
    public void Every_error_key_returned_by_the_core_services_has_a_translation()
    {
        var english = Store.Keys("en").ToHashSet();
        var coreFiles = Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "ExpenseControl.Core"), "*.cs", SearchOption.AllDirectories);
        var errorKeys = coreFiles
            .SelectMany(file => ErrorKeyRegex().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value))
            .ToHashSet();

        Assert.NotEmpty(errorKeys);
        Assert.Empty(errorKeys.Except(english));
    }

    [Fact]
    public void Dynamic_key_families_are_complete()
    {
        var english = Store.Keys("en").ToHashSet();

        var expected = new List<string>();
        expected.AddRange(Categories.AllKeys.Select(k => "category." + k));
        expected.AddRange(new[] { "thisMonth", "lastMonth", "last30", "last90", "thisYear", "custom" }.Select(p => "period." + p));
        expected.AddRange(new[] { "new", "edit" }.SelectMany(m => new[] { "expense", "income" }.Select(t => $"entry.{m}.{t}")));
        expected.AddRange(new[] { "type.expense", "type.income", "type.expenses", "type.incomes" });

        Assert.Empty(expected.Except(english));
    }

    [Fact]
    public void Unknown_keys_and_languages_fall_back_without_throwing()
    {
        Assert.Equal("no.such.key", Store.Get("pt", "no.such.key"));
        Assert.Equal(Store.Get("en", "login.submit"), Store.Get("xx", "login.submit"));
        Assert.Equal("en", TranslationStore.Find("xx").Code);
    }

    private static HashSet<string> Placeholders(string text) =>
        PlaceholderRegex().Matches(text).Select(m => m.Groups[1].Value).ToHashSet();

    private static HashSet<string> LiteralKeysUsedBySources()
    {
        var web = Path.Combine(RepositoryRoot(), "src", "ExpenseControl.Web");
        var files = Directory.EnumerateFiles(web, "*.razor", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(web, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        return files
            .SelectMany(file => UsedKeyRegex().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value))
            .ToHashSet();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExpenseControl.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (ExpenseControl.sln) not found.");
    }

    // L["some.key"] and L["some.key", args...]: only plain literals; dynamic keys are covered by Dynamic_key_families_are_complete.
    [GeneratedRegex("""\bL\["([A-Za-z0-9_.]+)"[,\]]""")]
    private static partial Regex UsedKeyRegex();

    [GeneratedRegex("\"(error\\.[A-Za-z]+)\"")]
    private static partial Regex ErrorKeyRegex();

    [GeneratedRegex(@"\{(\d+)(?:[:,][^}]*)?\}")]
    private static partial Regex PlaceholderRegex();
}
