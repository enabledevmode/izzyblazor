using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor;

/// <summary>What a <c>Table</c> does when its columns don't fit.</summary>
public enum TableSmallScreen
{
    /// <summary>It scrolls sideways. The default.</summary>
    Scroll,

    /// <summary>Each row becomes a card of lines, a column's title beside its value.</summary>
    Stack,
}

// Stacking a table when it's narrow: container queries in the table's own style, worked out on the
// server, so it's right at the first paint with or without script.
/// <inheritdoc />
public partial class Table<TItem>
{
    /// <summary>
    /// What it does when it's narrow: <see cref="TableSmallScreen.Scroll"/> (the default) scrolls
    /// sideways; <see cref="TableSmallScreen.Stack"/> makes each row a card of lines, a column's title
    /// beside its value, with the column titles and their sort and filter buttons in a row above. It goes by the
    /// table's own width, below <see cref="StackBelow"/>.
    /// </summary>
    [Parameter] public TableSmallScreen SmallScreen { get; set; }

    /// <summary>
    /// With <see cref="TableSmallScreen.Stack"/>, the table's width below which it stacks, such as
    /// <c>Length.Rem(40)</c>. Defaults to about what its columns need: each one's <c>Width</c>, or 8rem.
    /// </summary>
    [Parameter] public Length? StackBelow { get; set; }

    /// <summary>
    /// A CSP nonce for the style element that holds where it stacks. Defaults to the
    /// <see cref="IzzyTheme"/>'s <c>Nonce</c> on the page; set it on interactive pages, which don't
    /// see the theme's.
    /// </summary>
    [Parameter] public string? Nonce { get; set; }

    private string? _tableId;

    private bool Stacks => SmallScreen == TableSmallScreen.Stack && Shown.Count > 0;

    /// <inheritdoc />
    internal override string? GridId => Stacks ? _tableId ??= IdGenerator.Next("ib-table") : null;

    // Its parts become blocks, which can lose a table's meaning in some browsers: written out, it stays.
    /// <inheritdoc />
    internal override string? StackRole(string role) => Stacks ? role : null;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (StackBelow is not null && SmallScreen != TableSmallScreen.Stack)
        {
            throw new InvalidOperationException($"The Table sets {nameof(StackBelow)}, which only a Table with {nameof(SmallScreen)}=\"Stack\" takes.");
        }
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        // It goes by the width it's given, so it takes all of it rather than its columns' width.
        classes.Add("ib-table-fill", SmallScreen == TableSmallScreen.Stack);
        base.BuildAttributes(classes, styles);
    }

    private string StackWidth
    {
        get
        {
            if (StackBelow is { } set)
            {
                return StyleValues.Length(set);
            }

            var rem = (Selectable ? 2.75 : 0) + Shown.Sum(c => c.Width is { } width ? RemLength.Of(StyleValues.Length(width)) ?? 8 : 8);
            return rem.ToString("0.###", CultureInfo.InvariantCulture) + "rem";
        }
    }

    private string StackCss()
    {
        var t = "#" + GridId;
        var rows = $"{t}>:is(tbody,tfoot)>tr";
        var css = new StringBuilder("@layer izzy.components{@container (width < ").Append(StackWidth).Append("){");
        css.Append(CultureInfo.InvariantCulture, $"{t},{t}>*,{t}>*>tr{{display:block}}");
        // The header's buttons, in a row that wraps.
        css.Append(CultureInfo.InvariantCulture, $"{t}>thead>tr{{display:flex;flex-wrap:wrap;align-items:center;gap:var(--ib-space-1) var(--ib-space-3);padding:var(--ib-table-py) var(--ib-table-px);border-block-end:1px solid var(--ib-color-border)}}");
        css.Append(CultureInfo.InvariantCulture, $"{t}>thead>tr>th{{display:block;width:auto;padding:0;border:0}}");
        css.Append(CultureInfo.InvariantCulture, $"{rows}{{padding-block:var(--ib-table-py);border-block-end:1px solid var(--ib-color-border)}}{rows}:last-child{{border-block-end:0}}");
        // The title floats at the start, and the cell's content, however many parts, flows at the end.
        css.Append(CultureInfo.InvariantCulture, $"{rows}>td{{display:flow-root;padding-block:var(--ib-space-1);border:0;text-align:end}}");
        css.Append(CultureInfo.InvariantCulture, $"{rows}>td:not([colspan])::before{{float:inline-start;margin-inline-end:var(--ib-space-3);color:var(--ib-color-muted);text-align:start}}");

        // Each column's title beside its value: one rule a column, not a label on every cell. The
        // alternative text keeps screen readers from reading it as well as the column's header.
        var offset = Selectable ? 1 : 0;
        for (var i = 0; i < Shown.Count; i++)
        {
            var title = CssString(Shown[i].HeaderText);
            css.Append(CultureInfo.InvariantCulture, $"{rows}>td:nth-child({i + offset + 1}):not([colspan])::before{{content:{title};content:{title}/\"\"}}");
        }

        return css.Append("}}").ToString();
    }

    // Text as a CSS string, which can't end the string or the style element.
    private static string CssString(string text) =>
        "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("<", "\\3c ").Replace("\n", " ").Replace("\r", " ") + "\"";

    /// <inheritdoc />
    internal override RenderFragment? GridStyleSheet => !Stacks ? null : builder =>
    {
        builder.OpenElement(0, "style");
        builder.AddAttribute(1, "nonce", Nonce ?? Services.GetService<StyleNonce>()?.Value);
        builder.AddMarkupContent(2, StackCss());
        builder.CloseElement();
    };
}

/// <summary>A CSS length in rem, at a 16px root, where the server can tell.</summary>
internal static partial class RemLength
{
    /// <summary>The length in rem, or null for one that isn't known on the server.</summary>
    public static double? Of(string css)
    {
        var match = Pattern().Match(css.Trim());
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
    private static partial Regex Pattern();
}
