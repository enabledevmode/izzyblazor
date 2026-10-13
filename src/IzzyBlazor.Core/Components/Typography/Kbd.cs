using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A key or a key combination, as people type it: <c>kbd</c> elements, nested for a combination
/// the way the HTML spec describes. Sized in em, so it fits the text around it.
/// </summary>
/// <example>
/// <code>
/// Press &lt;Kbd&gt;Esc&lt;/Kbd&gt; to close.
/// &lt;Kbd Keys="Ctrl+K" /&gt;
/// &lt;Kbd Keys="⌘+Shift+P" /&gt;
/// </code>
/// </example>
public class Kbd : IzzyComponentBase
{
    /// <summary>One key, such as "Esc". For a combination, use <see cref="Keys"/>.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// A combination, keys joined with "+", such as "Ctrl+K" or "Ctrl+Shift+P". Spaces around the
    /// "+" are ignored, and a "+" at the end is the plus key ("Ctrl++"). Each key gets its own box.
    /// Empty or blank falls back to <see cref="ChildContent"/>.
    /// </summary>
    [Parameter] public string? Keys { get; set; }

    /// <summary>What's shown between the keys of a combination. Defaults to "+".</summary>
    [Parameter] public string Separator { get; set; } = "+";

    /// <summary>Splits a combination such as "Ctrl+Shift+P" or "Ctrl++" into its keys.</summary>
    internal static IReadOnlyList<string> SplitKeys(string? keys)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(keys))
        {
            return result;
        }

        var i = 0;
        while (i < keys.Length)
        {
            while (i < keys.Length && char.IsWhiteSpace(keys[i]))
            {
                i++;
            }

            if (i >= keys.Length)
            {
                break;
            }

            int end;
            if (keys[i] == '+')
            {
                end = i + 1;
            }
            else
            {
                end = keys.IndexOf('+', i);
                if (end < 0)
                {
                    end = keys.Length;
                }
            }

            var key = keys[i..end].Trim();
            if (key.Length > 0)
            {
                result.Add(key);
            }

            // Skip the separator after the key.
            i = end;
            while (i < keys.Length && char.IsWhiteSpace(keys[i]))
            {
                i++;
            }

            // A "+" is the separator when another key follows it; a trailing one is the plus key.
            if (i < keys.Length && keys[i] == '+' && keys.AsSpan(i + 1).Trim().Length > 0)
            {
                i++;
            }
        }

        return result;
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles) =>
        classes.Add(string.IsNullOrWhiteSpace(Keys) ? "ib-kbd" : "ib-kbd-combo");

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "kbd");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        if (string.IsNullOrWhiteSpace(Keys))
        {
            builder.AddContent(4, ChildContent);
        }
        else
        {
            var keys = SplitKeys(Keys);
            for (var k = 0; k < keys.Count; k++)
            {
                if (k > 0)
                {
                    builder.OpenElement(5, "span");
                    builder.AddAttribute(6, "class", "ib-kbd-separator");
                    builder.AddContent(7, Separator);
                    builder.CloseElement();
                }

                builder.OpenElement(8, "kbd");
                builder.AddAttribute(9, "class", "ib-kbd");
                builder.AddContent(10, keys[k]);
                builder.CloseElement();
            }
        }

        builder.CloseElement();
    }
}
