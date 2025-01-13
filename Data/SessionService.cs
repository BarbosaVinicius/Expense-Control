using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

public class SessionService
{
    private readonly IJSRuntime _jsRuntime;
    private Action _onTimeout;

    public SessionService(IJSRuntime jSRuntime)
    {
        _jsRuntime = jSRuntime;
    }

    public async Task SetSessionAsync(string username)
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "username", username);
    }

    public async Task<string> GetSessionAsync()
    {
        return await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "username");
    }

    public async Task ClearSessionAsync()
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "username");
    }

    public async Task StartInactivityTrackingAsync(Action onTimeout)
    {
        _onTimeout = onTimeout;
        await _jsRuntime.InvokeVoidAsync("startInactivityTime", DotNetObjectReference.Create(this), 30 * 60 * 1000);
    }

    [JSInvokable]
    public void OnInactivityTimeout()
    {
        _onTimeout?.Invoke();
    }
}