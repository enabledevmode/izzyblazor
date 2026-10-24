namespace IzzyBlazor;

/// <summary>
/// The CSP nonce the app's <see cref="IzzyTheme"/> was given, for the other style elements
/// IzzyBlazor writes (a Toolbar's widths) in the same request.
/// </summary>
internal sealed class StyleNonce
{
    public string? Value { get; set; }
}
