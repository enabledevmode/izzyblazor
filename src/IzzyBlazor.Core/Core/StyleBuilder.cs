using System.Text;

namespace IzzyBlazor;

/// <summary>
/// Builds an inline <c>style</c> string of IzzyBlazor custom properties
/// (<c>--ib-px:...; --ib-px-md:...;</c>) followed by any user style.
/// </summary>
public sealed class StyleBuilder
{
    private readonly StringBuilder _sb = new();

    /// <summary>
    /// Adds one custom property per value that is set: <c>--ib-{name}</c> for the base value and
    /// <c>--ib-{name}-{breakpoint}</c> for each breakpoint, each mapped to CSS by <paramref name="map"/>.
    /// </summary>
    public StyleBuilder AddVars<T>(string name, Responsive<T> value, Func<T, string> map)
    {
        foreach (var (breakpoint, item) in value.Entries)
        {
            _sb.Append("--ib-").Append(name);
            if (breakpoint is { } bp)
            {
                _sb.Append('-').Append(bp.Name());
            }

            _sb.Append(':').Append(map(item)).Append(';');
        }

        return this;
    }

    /// <summary>Adds a single custom property, <c>--ib-{name}:{value};</c>.</summary>
    public StyleBuilder AddVar(string name, string value)
    {
        _sb.Append("--ib-").Append(name).Append(':').Append(value).Append(';');
        return this;
    }

    /// <summary>Appends a raw style string, such as the user's <c>Style</c> parameter.</summary>
    public StyleBuilder AddRaw(string? style)
    {
        if (!string.IsNullOrWhiteSpace(style))
        {
            var trimmed = style.Trim();
            _sb.Append(trimmed);
            if (!trimmed.EndsWith(';'))
            {
                _sb.Append(';');
            }
        }

        return this;
    }

    /// <summary>The style string, or <c>null</c> when nothing was added (so no attribute renders).</summary>
    public string? Build() => _sb.Length == 0 ? null : _sb.ToString();

    /// <inheritdoc />
    public override string ToString() => _sb.ToString();
}
