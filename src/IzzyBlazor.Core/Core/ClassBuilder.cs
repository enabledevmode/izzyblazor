using System.Text;

namespace IzzyBlazor;

/// <summary>Builds a space-separated <c>class</c> attribute value without duplicate spaces.</summary>
public sealed class ClassBuilder
{
    private readonly StringBuilder _sb = new();

    /// <summary>Adds a class (or several, space-separated). Null or whitespace is ignored.</summary>
    public ClassBuilder Add(string? cssClass)
    {
        if (!string.IsNullOrWhiteSpace(cssClass))
        {
            if (_sb.Length > 0)
            {
                _sb.Append(' ');
            }

            _sb.Append(cssClass.Trim());
        }

        return this;
    }

    /// <summary>Adds a class only when <paramref name="when"/> is true.</summary>
    public ClassBuilder Add(string cssClass, bool when) => when ? Add(cssClass) : this;

    /// <summary>The class string, or <c>null</c> when nothing was added.</summary>
    public string? Build() => _sb.Length == 0 ? null : _sb.ToString();

    /// <inheritdoc />
    public override string ToString() => _sb.ToString();
}
