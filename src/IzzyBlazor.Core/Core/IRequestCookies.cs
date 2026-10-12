namespace IzzyBlazor;

/// <summary>
/// Reads the current request's cookies while a page renders on the server. AppSidebar uses it to
/// render in its remembered collapsed state. The IzzyBlazor package registers one; in
/// WebAssembly there's no request, and components fall back to their defaults.
/// </summary>
public interface IRequestCookies
{
    /// <summary>The cookie's value, or null when it's missing or there's no request to read.</summary>
    string? Get(string name);
}
