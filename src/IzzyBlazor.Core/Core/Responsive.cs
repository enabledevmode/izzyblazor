namespace IzzyBlazor;

/// <summary>
/// A value that can change at each breakpoint. Pass a single value, or a map with
/// <see cref="Base"/> and any breakpoints you need.
/// </summary>
/// <example>
/// <code>
/// &lt;Stack Gap="4"&gt;                                        @* single value *@
/// &lt;Stack Gap="new() { Base = 2, Md = 6 }"&gt;                 @* per breakpoint *@
/// &lt;Stack Direction="new() { Base = Direction.Column, Md = Direction.Row }"&gt;
/// </code>
/// </example>
/// <remarks>
/// Only the breakpoints you set produce CSS, so a map with only <see cref="Md"/> keeps the
/// component's default below 768px. The default value is empty and produces nothing.
/// </remarks>
public readonly struct Responsive<T>
{
    private readonly T _base;
    private readonly T _sm;
    private readonly T _md;
    private readonly T _lg;
    private readonly T _xl;
    private readonly byte _set;

    /// <summary>The value at every width, unless a breakpoint overrides it.</summary>
    public T Base { get => _base; init { _base = value; _set |= 1; } }

    /// <summary>The value from 640px up.</summary>
    public T Sm { get => _sm; init { _sm = value; _set |= 2; } }

    /// <summary>The value from 768px up.</summary>
    public T Md { get => _md; init { _md = value; _set |= 4; } }

    /// <summary>The value from 1024px up.</summary>
    public T Lg { get => _lg; init { _lg = value; _set |= 8; } }

    /// <summary>The value from 1280px up.</summary>
    public T Xl { get => _xl; init { _xl = value; _set |= 16; } }

    /// <summary>Whether no value is set at all.</summary>
    public bool IsEmpty => _set == 0;

    /// <summary>The values that are set, base first, then breakpoints smallest first.</summary>
    public IEnumerable<(Breakpoint? Breakpoint, T Value)> Entries
    {
        get
        {
            if ((_set & 1) != 0) yield return (null, _base);
            if ((_set & 2) != 0) yield return (Breakpoint.Sm, _sm);
            if ((_set & 4) != 0) yield return (Breakpoint.Md, _md);
            if ((_set & 8) != 0) yield return (Breakpoint.Lg, _lg);
            if ((_set & 16) != 0) yield return (Breakpoint.Xl, _xl);
        }
    }

    /// <summary>
    /// Builds a new responsive value from this one, mapping each value that is set. Values the
    /// mapper returns <c>null</c> for are left unset.
    /// </summary>
    public Responsive<TOut> Select<TOut>(Func<T, TOut?> map) where TOut : struct
    {
        var result = default(Responsive<TOut>);
        foreach (var (breakpoint, value) in Entries)
        {
            if (map(value) is not { } mapped)
            {
                continue;
            }

            result = breakpoint switch
            {
                null => result with { Base = mapped },
                Breakpoint.Sm => result with { Sm = mapped },
                Breakpoint.Md => result with { Md = mapped },
                Breakpoint.Lg => result with { Lg = mapped },
                _ => result with { Xl = mapped },
            };
        }

        return result;
    }

    /// <summary>A single value that applies at every width.</summary>
    public static implicit operator Responsive<T>(T value) => new() { Base = value };

    /// <inheritdoc />
    public override string ToString() =>
        IsEmpty ? "(empty)" : string.Join(" ", Entries.Select(e => e.Breakpoint is null ? $"{e.Value}" : $"{e.Breakpoint.Value.Name()}:{e.Value}"));
}
