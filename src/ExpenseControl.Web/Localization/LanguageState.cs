using System.Globalization;
using Microsoft.JSInterop;

namespace ExpenseControl.Web.Localization;

/// <summary>
/// The language of the current browser tab. Saved in the browser (so it survives reloads and is shared by all tabs)
/// and announced through <see cref="Changed"/> so every component can redraw its texts.
/// </summary>
public sealed class LanguageState(IJSRuntime js)
{
    private Language _language = TranslationStore.Find(null);

    public event Action? Changed;

    public bool IsInitialized { get; private set; }

    public string Code => _language.Code;

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo(TranslationStore.Find(null).Culture);

    /// <summary>Reads the saved language (or the browser's) once per circuit.</summary>
    public async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        try
        {
            Apply(await js.InvokeAsync<string>("ecPrefs.getLanguage"));
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException or JSException)
        {
            // JS interop is not available: keep the default language.
        }

        IsInitialized = true;
    }

    public async Task SetAsync(string code)
    {
        Apply(code);
        try
        {
            await js.InvokeVoidAsync("ecPrefs.setLanguage", Code);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
            // The choice still applies to this session even if it cannot be saved.
        }

        Changed?.Invoke();
    }

    private void Apply(string? code)
    {
        _language = TranslationStore.Find(code);
        Culture = CultureInfo.GetCultureInfo(_language.Culture);
    }
}
