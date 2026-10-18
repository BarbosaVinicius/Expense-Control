using Microsoft.AspNetCore.Components;

namespace ExpenseControl.Web.Localization;

/// <summary>Base of every page and component that shows text: provides <c>L</c> and <c>Fmt</c> and redraws on language change.</summary>
public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    private bool _subscribed;

    [Inject] protected LanguageState Language { get; set; } = default!;

    [Inject] protected Localizer L { get; set; } = default!;

    [Inject] protected Formatter Fmt { get; set; } = default!;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (!_subscribed)
        {
            Language.Changed += OnLanguageChanged;
            _subscribed = true;
        }

        return base.SetParametersAsync(parameters);
    }

    private void OnLanguageChanged() => _ = InvokeAsync(StateHasChanged);

    public virtual void Dispose()
    {
        Language.Changed -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}

/// <summary>Same as <see cref="LocalizedComponentBase"/>, for layouts.</summary>
public abstract class LocalizedLayoutBase : LayoutComponentBase, IDisposable
{
    private bool _subscribed;

    [Inject] protected LanguageState Language { get; set; } = default!;

    [Inject] protected Localizer L { get; set; } = default!;

    [Inject] protected Formatter Fmt { get; set; } = default!;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (!_subscribed)
        {
            Language.Changed += OnLanguageChanged;
            _subscribed = true;
        }

        return base.SetParametersAsync(parameters);
    }

    private void OnLanguageChanged() => _ = InvokeAsync(StateHasChanged);

    public virtual void Dispose()
    {
        Language.Changed -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
