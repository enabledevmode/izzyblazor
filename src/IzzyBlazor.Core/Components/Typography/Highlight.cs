using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// Text with the words of a search marked: each match in a <c>mark</c>, for search results, a
/// filtered list or a table filtered by a <see cref="FilterForm"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;Highlight Text="@product.Name" Query="@Query" /&gt;
/// &lt;Highlight Text="@product.Description" Query="@Query" Match="HighlightMatch.Anywhere" /&gt;
/// </code>
/// </example>
/// <remarks>
/// The query is split into words at spaces and punctuation, and each word is marked wherever it
/// matches, ignoring case. By default a word matches the start of a word in the text, and the
/// words inside a name written in PascalCase count: "field" marks the "Field" of "TextField".
/// Overlapping matches are marked as one.
/// </remarks>
/// <accessibility>
/// <list type="bullet">
/// <item>The text stays one run of text; matches are <c>mark</c> elements inside it, which some screen readers announce as highlighted.</item>
/// <item>The marks are a tint behind the text, which keeps its color, so the contrast is the text's own.</item>
/// </list>
/// </accessibility>
/// <seealso cref="FilterForm"/>
/// <seealso cref="Table{TItem}"/>
[ModeSupport]
public class Highlight : LayoutItemBase
{
    /// <summary>The text to show.</summary>
    [Parameter, EditorRequired] public string? Text { get; set; }

    /// <summary>The search whose words are marked. Empty or blank marks nothing.</summary>
    [Parameter] public string? Query { get; set; }

    /// <summary>Where a word of the query may match: at the start of a word (the default) or anywhere.</summary>
    [Parameter] public HighlightMatch Match { get; set; }

    /// <summary>
    /// The ranges of <paramref name="text"/> that the words of <paramref name="query"/> match, in
    /// order, with overlapping and touching ranges joined.
    /// </summary>
    public static IReadOnlyList<Range> Find(string? text, string? query, HighlightMatch match = HighlightMatch.WordStart)
    {
        var ranges = new List<(int Start, int End)>();
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        foreach (var word in Words(query))
        {
            for (var at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0;
                 at = text.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
            {
                if (match == HighlightMatch.Anywhere || StartsWord(text, at))
                {
                    ranges.Add((at, at + word.Length));
                }
            }
        }

        ranges.Sort();
        var joined = new List<Range>();
        var (start, end) = (-1, -1);
        foreach (var (s, e) in ranges)
        {
            if (s <= end)
            {
                end = Math.Max(end, e);
                continue;
            }

            if (start >= 0)
            {
                joined.Add(start..end);
            }

            (start, end) = (s, e);
        }

        if (start >= 0)
        {
            joined.Add(start..end);
        }

        return joined;
    }

    // The query's words: runs of letters and digits.
    private static IEnumerable<string> Words(string query)
    {
        var start = -1;
        for (var i = 0; i <= query.Length; i++)
        {
            var inWord = i < query.Length && char.IsLetterOrDigit(query[i]);
            if (inWord && start < 0)
            {
                start = i;
            }
            else if (!inWord && start >= 0)
            {
                yield return query[start..i];
                start = -1;
            }
        }
    }

    // A word starts after anything that isn't a letter or digit, at a capital after a lowercase
    // letter or digit ("Text|Field"), and at the last capital of a run before lowercase ("HTML|Editor").
    private static bool StartsWord(string text, int at)
    {
        if (at == 0)
        {
            return true;
        }

        var previous = text[at - 1];
        var current = text[at];
        if (!char.IsLetterOrDigit(previous))
        {
            return true;
        }

        if (char.IsUpper(current) && (char.IsLower(previous) || char.IsDigit(previous)))
        {
            return true;
        }

        return char.IsUpper(current) && char.IsUpper(previous) && at + 1 < text.Length && char.IsLower(text[at + 1]);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-highlight");
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "span");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        var text = Text ?? "";
        var from = 0;
        foreach (var range in Find(text, Query, Match))
        {
            var (start, length) = range.GetOffsetAndLength(text.Length);
            builder.AddContent(4, text[from..start]);
            builder.OpenElement(5, "mark");
            builder.AddAttribute(6, "class", "ib-mark");
            builder.AddContent(7, text.Substring(start, length));
            builder.CloseElement();
            from = start + length;
        }

        builder.AddContent(8, text[from..]);
        builder.CloseElement();
    }
}

/// <summary>Where a word of a <see cref="Highlight"/>'s query may match.</summary>
public enum HighlightMatch
{
    /// <summary>At the start of a word, including the words inside a PascalCase name.</summary>
    WordStart,

    /// <summary>Anywhere in the text.</summary>
    Anywhere,
}
