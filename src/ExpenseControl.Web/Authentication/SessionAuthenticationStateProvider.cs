using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace ExpenseControl.Web.Authentication;

/// <summary>
/// Keeps the signed-in user in the browser's session storage, protected with ASP.NET Core Data Protection
/// (encrypted and signed), so the value cannot be read or forged from the browser. The session lasts as long
/// as the tab and is also limited by <see cref="SessionOptions.MaxSessionHours"/>.
/// </summary>
/// <remarks>
/// Requires an interactive circuit (JS interop), so the host page must not prerender components.
/// In production with several instances, share the Data Protection keys between them.
/// </remarks>
public sealed class SessionAuthenticationStateProvider(
    ProtectedSessionStorage storage,
    IOptions<SessionOptions> options,
    TimeProvider clock) : AuthenticationStateProvider
{
    public const string AuthenticationType = "ExpenseControlSession";

    private const string StorageKey = "expensecontrol.session";

    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private SessionTicket? _ticket;
    private bool _loaded;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_loaded)
        {
            try
            {
                var stored = await storage.GetAsync<SessionTicket>(StorageKey);
                _ticket = stored.Success ? stored.Value : null;
                _loaded = true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException)
            {
                // JS interop is not available yet (prerendering) or the circuit is gone: treat as signed out for now.
                return Anonymous;
            }
        }

        return ToState(_ticket);
    }

    public async Task SignInAsync(string username)
    {
        var ticket = new SessionTicket(username, clock.GetUtcNow().AddHours(options.Value.MaxSessionHours));
        await storage.SetAsync(StorageKey, ticket);

        _ticket = ticket;
        _loaded = true;
        NotifyAuthenticationStateChanged(Task.FromResult(ToState(ticket)));
    }

    public async Task SignOutAsync()
    {
        try
        {
            await storage.DeleteAsync(StorageKey);
        }
        catch (JSDisconnectedException)
        {
            // The browser is already gone; there is nothing left to clear.
        }

        _ticket = null;
        _loaded = true;
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private AuthenticationState ToState(SessionTicket? ticket)
    {
        if (ticket is null || string.IsNullOrEmpty(ticket.Username) || ticket.ExpiresAt <= clock.GetUtcNow())
        {
            return Anonymous;
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, ticket.Username)], AuthenticationType);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private sealed record SessionTicket(string Username, DateTimeOffset ExpiresAt);
}
