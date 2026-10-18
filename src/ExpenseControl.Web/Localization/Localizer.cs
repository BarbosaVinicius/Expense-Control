using ExpenseControl.Core.Common;
using ExpenseControl.Core.Domain;

namespace ExpenseControl.Web.Localization;

/// <summary>Looks texts up in the current language: <c>L["login.submit"]</c>, <c>L["budget.spentOf", spent, limit]</c>.</summary>
public sealed class Localizer(LanguageState state, TranslationStore store)
{
    public string this[string key] => store.Get(state.Code, key);

    /// <summary>The text formatted with <paramref name="args"/>, using the current culture for numbers and dates.</summary>
    public string this[string key, params object[] args] => string.Format(state.Culture, store.Get(state.Code, key), args);

    /// <summary>The user-facing message for a failed <see cref="OperationResult"/> (its error is a translation key).</summary>
    public string Error(OperationResult result) =>
        result.Error is null ? string.Empty : this[result.Error, [.. result.ErrorArgs]];

    public string Category(string key) => this["category." + key];

    public string TypeName(EntryType type) => this[type == EntryType.Expense ? "type.expense" : "type.income"];
}
