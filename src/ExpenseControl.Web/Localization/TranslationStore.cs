using System.Reflection;
using System.Text.Json;

namespace ExpenseControl.Web.Localization;

/// <param name="Code">Short code stored in the browser ("pt", "en", "es").</param>
/// <param name="Culture">.NET culture used to format dates, numbers and currency.</param>
/// <param name="NativeName">Name shown in the language picker, in that language.</param>
public sealed record Language(string Code, string Culture, string NativeName);

/// <summary>
/// All UI texts, loaded once from the embedded <c>Localization/Resources/{code}.json</c> files.
/// To add a language: add its JSON file (copy en.json), then list it in <see cref="Languages"/>.
/// </summary>
public sealed class TranslationStore
{
    public const string DefaultCode = "en";

    public static IReadOnlyList<Language> Languages { get; } =
    [
        new("pt", "pt-BR", "Português"),
        new("en", "en-US", "English"),
        new("es", "es-ES", "Español")
    ];

    private readonly Dictionary<string, Dictionary<string, string>> _texts = new(StringComparer.Ordinal);

    public TranslationStore()
    {
        var assembly = typeof(TranslationStore).Assembly;
        foreach (var language in Languages)
        {
            var resourceName = $"{typeof(TranslationStore).Namespace}.Resources.{language.Code}.json";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing embedded translation file '{resourceName}'.");
            _texts[language.Code] = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                ?? throw new InvalidOperationException($"Translation file '{resourceName}' is empty.");
        }
    }

    public static Language Find(string? code) =>
        Languages.FirstOrDefault(l => l.Code == code) ?? Languages.First(l => l.Code == DefaultCode);

    public IReadOnlyCollection<string> Keys(string code) => _texts[code].Keys;

    /// <summary>The text in the given language; falls back to English, then to the key itself so a gap is visible but harmless.</summary>
    public string Get(string code, string key)
    {
        if (_texts.TryGetValue(code, out var texts) && texts.TryGetValue(key, out var text))
        {
            return text;
        }

        return _texts[DefaultCode].TryGetValue(key, out var fallback) ? fallback : key;
    }
}
