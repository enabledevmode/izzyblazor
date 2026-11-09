using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// One level of a chart's drill-down, such as years, then months, then days: in a
/// <see cref="Chart{TItem}"/>, in order from the top. The chart groups its items by the level's
/// <see cref="Argument"/>, adds up each series by its <c>Aggregate</c>, and links each bar or
/// point to the next level down; a breadcrumb above the plot goes back up. The level is in the
/// page's address (<see cref="Chart{TItem}.Drill"/>), so it works with no script, the browser's
/// Back goes up a level, and a level can be bookmarked.
/// <para>
/// <c>&lt;ChartDrillLevel Argument="s =&gt; s.Date.Year" Name="Year" /&gt;</c>
/// </para>
/// </summary>
/// <typeparam name="TItem">The chart's items.</typeparam>
[ModeSupport]
public sealed class ChartDrillLevel<TItem> : ComponentBase, IDisposable
{
    private Expression<Func<TItem, object?>>? _last;

    [CascadingParameter] private ChartContext<TItem>? Context { get; set; }

    /// <summary>What the level groups the items by, such as <c>s =&gt; s.Date.Year</c>: names, numbers or dates.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Argument { get; set; } = default!;

    /// <summary>What the level's arguments are, such as "Year", for the breadcrumb and the hints ("by year"). Defaults to the property's name in words.</summary>
    [Parameter] public string? Name { get; set; }

    internal Func<TItem, object?> Of { get; private set; } = default!;

    internal string DisplayName { get; private set; } = "";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null) throw new InvalidOperationException("ChartDrillLevel goes inside a Chart, one for each level from the top.");
        Context.Register(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Argument is null) throw new InvalidOperationException("ChartDrillLevel needs an Argument, what the level groups by, such as s => s.Date.Year.");
        if (!ReferenceEquals(Argument, _last))
        {
            _last = Argument;
            Of = Argument.Compile();
        }

        DisplayName = Name ?? ChartMembers.Name(Argument) ?? "";
        Context!.Seen(this);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose() => Context?.Unregister(this);
}

/// <summary>A drill-down level's key in the address: an argument as invariant text.</summary>
internal static class ChartDrillKey
{
    public static string Of(object? value) => value switch
    {
        null => "",
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : d.ToString("s", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToString("s", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
