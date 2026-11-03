using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor;

/// <summary>What a <c>DataGrid</c> does when its columns don't fit.</summary>
public enum DataGridSmallScreen
{
    /// <summary>It scrolls sideways. The default.</summary>
    Scroll,

    /// <summary>
    /// Columns hide, lowest <c>Priority</c> first, and their values show in the row's detail, opened by
    /// the button at the row's start.
    /// </summary>
    HideColumns,
}

// Hiding columns when the grid is narrow. The grid's columns have fixed widths, so the server knows
// the width each set of columns needs, and writes container queries for them into the grid's own
// style: right at the first paint, with or without script. A hidden column keeps its place at no
// width, so cells that span columns (group labels, details) still line up.
public partial class DataGrid<TItem>
{
    /// <summary>
    /// What it does when its columns don't fit its width: <see cref="DataGridSmallScreen.Scroll"/> (the
    /// default) scrolls sideways; <see cref="DataGridSmallScreen.HideColumns"/> hides columns by their
    /// <c>Priority</c> and shows their values in the row's detail. Hiding needs an <c>ItemKey</c>.
    /// </summary>
    [Parameter] public DataGridSmallScreen SmallScreen { get; set; }

    /// <summary>
    /// A CSP nonce for the style element that holds the widths where columns hide. Defaults to the
    /// <see cref="IzzyTheme"/>'s <c>Nonce</c> on the page; set it on interactive pages, which don't
    /// see the theme's.
    /// </summary>
    [Parameter] public string? Nonce { get; set; }

    private string? _tableId;
    private HidePlan? _hidePlan;
    private string? _hidePlanFor;

    internal bool Hiding => SmallScreen == DataGridSmallScreen.HideColumns && Shown.Count > 1;

    // A detail button for each row: for a DetailTemplate, or for the values of hidden columns.
    private bool HasExpander => HasDetails || Hiding;

    // Hiding with no template: the button column shows only while a column is hidden.
    private bool ExpanderComesAndGoes => Hiding && !HasDetails;

    /// <inheritdoc />
    internal override string? GridId => Hiding ? _tableId ??= IdGenerator.Next("ib-grid") : null;

    /// <summary>The order columns hide in, and the grid's width with each one gone.</summary>
    /// <param name="Levels">Each column that can hide, by its level: 1 hides first.</param>
    /// <param name="Widths">The grid's width in rem with that many hidden; [0] with none.</param>
    private sealed record HidePlan(Dictionary<ColumnBase<TItem>, int> Levels, double[] Widths);

    private HidePlan Plan
    {
        get
        {
            var signature = string.Join('|', Shown.Select(c => $"{c.Priority}:{WidthOf(c)}:{c.MinWidth}:{FrozenOf(c)}"))
                + $"|{Selectable}|{Grouping.Levels.Count}|{HasDetails}|{(HasActionCell ? ActionWidth : 0)}";
            if (_hidePlan is null || _hidePlanFor != signature)
            {
                (_hidePlanFor, _hidePlan) = (signature, MakePlan());
            }

            return _hidePlan;
        }
    }

    private HidePlan MakePlan()
    {
        // The first column names the row, and frozen ones are kept in view on purpose.
        var order = Shown.Select((column, index) => (column, index))
            .Where(c => c.index > 0 && FrozenOf(c.column) == ColumnFrozen.None)
            .OrderBy(c => c.column.Priority)
            .ThenByDescending(c => c.index)
            .Select(c => c.column)
            .ToList();

        var levels = new Dictionary<ColumnBase<TItem>, int>();
        for (var i = 0; i < order.Count; i++)
        {
            levels[order[i]] = i + 1;
        }

        var widths = new double[order.Count + 1];
        for (var hidden = 0; hidden <= order.Count; hidden++)
        {
            var gone = order.Take(hidden).ToHashSet();
            double set = 0;
            var unset = new List<double>();
            foreach (var column in Shown.Where(c => !gone.Contains(c)))
            {
                if (WidthOf(column) is { } width)
                {
                    set += Rem(width) ?? DefaultMinRem;
                }
                else
                {
                    unset.Add(column.MinWidth is { } min ? Rem(StyleValues.Length(min)) ?? DefaultMinRem : DefaultMinRem);
                }
            }

            // As the grid's own width is worked out (GridStyle): the columns without a width share
            // the rest equally, so each needs the largest of their minimums.
            var leads = Grouping.Levels.Count + (HasDetails || (ExpanderComesAndGoes && hidden > 0) ? 1 : 0);
            widths[hidden] = set
                + (unset.Count > 0 ? unset.Count * unset.Max() : 0)
                + (Selectable ? SelectRem : 0)
                + leads * LeadRem
                + (HasActionCell ? ActionWidth : 0);
        }

        return new HidePlan(levels, widths);
    }

    // Widths the stylesheet gives these columns (data.css). A container query can't read a variable.
    private const double SelectRem = 2.75;
    private const double LeadRem = 2.25;
    private const double DefaultMinRem = 8;

    // The action column's width in rem, as GridStyle sets it.
    private double ActionWidth
    {
        get
        {
            var buttons = ActionButtons;
            return buttons > 1 ? 0.75 + 2.25 * buttons : 3;
        }
    }

    // A length in rem, at a 16px root, or null for one that isn't known on the server.
    private static double? Rem(string css)
    {
        var match = LengthPattern().Match(css.Trim());
        if (!match.Success)
        {
            return null;
        }

        var value = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return match.Groups[2].Value switch
        {
            "px" => value / 16,
            "ch" => value / 2,
            _ => value,
        };
    }

    [GeneratedRegex(@"^(\d+(?:\.\d+)?)(px|rem|em|ch)$")]
    private static partial Regex LengthPattern();

    /// <summary>The level class of a column that can hide, for its cells and its col.</summary>
    private string? HideClass(ColumnBase<TItem> column) =>
        Hiding && Plan.Levels.TryGetValue(column, out var level) ? $"ib-h{level}" : null;

    // How a hidden cell goes: it keeps its place, at no width, and nothing in it shows or takes focus.
    private const string GoneCell = "visibility:hidden;padding:0;border:0;font-size:0;line-height:0";

    private string HideCss()
    {
        static string Rems(double rem) => rem.ToString("0.###", CultureInfo.InvariantCulture) + "rem";

        var plan = Plan;
        var t = "#" + GridId;
        var width = Shown.Any(c => WidthOf(c) is null) ? "min-width" : "width";
        var css = new StringBuilder("@layer izzy.components{");

        // Wide enough for every column: nothing is hidden, so neither are the detail's values, nor,
        // with no template, the buttons, their column and the details.
        var none = new StringBuilder();
        if (ExpanderComesAndGoes)
        {
            none.Append(CultureInfo.InvariantCulture, $"{t}{{--ib-grid-expander:0px}}{t}>*>tr>.ib-hx{{{GoneCell}}}{t}>*>tr>.ib-hx>*,{t}>tbody>.ib-hx{{display:none}}");
        }
        else
        {
            none.Append(CultureInfo.InvariantCulture, $"{t}>*>tr>*>.ib-hx,{t}>tbody>.ib-hx{{display:none}}");
        }

        for (var level = 1; level < plan.Widths.Length; level++)
        {
            // A column's value in a detail shows only while its column is hidden.
            css.Append(CultureInfo.InvariantCulture, $"@container (width >= {Rems(plan.Widths[level - 1])}){{{t}>*>tr>*>*>dl>.ib-h{level}{{display:none}}}}");
            css.Append(CultureInfo.InvariantCulture,
                $"@container (width < {Rems(plan.Widths[level - 1])}){{{t}>colgroup>.ib-h{level}{{width:0!important}}{t}>*>tr>.ib-h{level}{{{GoneCell}}}{t}>*>tr>.ib-h{level}>*{{display:none}}{t}{{{width}:{Rems(plan.Widths[level])}!important}}}}");
        }

        css.Append(CultureInfo.InvariantCulture, $"@container (width >= {Rems(plan.Widths[0])}){{{none}}}");
        return css.Append('}').ToString();
    }

    /// <inheritdoc />
    internal override RenderFragment? GridStyleSheet => !Hiding ? null : builder =>
    {
        builder.OpenElement(0, "style");
        builder.AddAttribute(1, "nonce", Nonce ?? Services.GetService<StyleNonce>()?.Value);
        builder.AddMarkupContent(2, HideCss());
        builder.CloseElement();
    };

    /// <summary>The hidden columns' values, at the start of an open row's detail.</summary>
    private RenderFragment HiddenValues(TItem item) => builder =>
    {
        var levels = Plan.Levels;
        builder.OpenElement(0, "dl");
        builder.AddAttribute(1, "class", "ib-grid-hidden");
        foreach (var column in Shown.Where(levels.ContainsKey))
        {
            builder.OpenElement(2, "div");
            builder.AddAttribute(3, "class", $"ib-h{levels[column]}");
            builder.OpenElement(4, "dt");
            builder.AddContent(5, column.HeaderText);
            builder.CloseElement();
            builder.OpenElement(6, "dd");
            builder.AddContent(7, column.Cell(item));
            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();
    };
}
