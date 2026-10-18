namespace ExpenseControl.Core.Common;

/// <summary>An e-mail ready to be sent. Its text is written by the UI layer, in the user's language.</summary>
public sealed record EmailMessage(string Subject, string HtmlBody);
