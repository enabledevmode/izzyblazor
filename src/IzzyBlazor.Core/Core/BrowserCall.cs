using Microsoft.JSInterop;

namespace IzzyBlazor;

/// <summary>
/// Calls the page's script from a service when the page has one to call: on an interactive page.
/// During static rendering (and prerendering) the JS runtime refuses the call at once, and the
/// caller renders what it had to say in the response instead.
/// </summary>
internal static class BrowserCall
{
    /// <summary>True when the call was sent; false when there's no browser to send it to.</summary>
    public static bool TrySend(IJSRuntime? js, string identifier, params object?[] args)
    {
        if (js is null)
        {
            return false;
        }

        ValueTask call;
        try
        {
            call = js.InvokeVoidAsync(identifier, args);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (call.IsCompleted)
        {
            return call.IsCompletedSuccessfully || call.AsTask().Exception?.InnerException is not InvalidOperationException;
        }

        _ = ObserveAsync(call);
        return true;
    }

    private static async Task ObserveAsync(ValueTask call)
    {
        try
        {
            await call;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException or InvalidOperationException)
        {
            // The circuit went away before the call arrived.
        }
    }
}
