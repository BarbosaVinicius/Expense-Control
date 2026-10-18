using System.ComponentModel.DataAnnotations;

namespace ExpenseControl.Web.Authentication;

public sealed class SessionOptions
{
    public const string SectionName = "Session";

    /// <summary>Minutes without mouse or keyboard activity before the user is signed out.</summary>
    [Range(1, 24 * 60)]
    public int InactivityTimeoutMinutes { get; set; } = 30;

    /// <summary>Hours after which a sign-in expires no matter how active the user is.</summary>
    [Range(1, 24 * 30)]
    public int MaxSessionHours { get; set; } = 12;
}
