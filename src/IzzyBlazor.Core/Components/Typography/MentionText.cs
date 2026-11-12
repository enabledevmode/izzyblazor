using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// Text written in a <see cref="Mentions{TItem}"/> field, shown with each mention marked, or linked
/// with <see cref="Href"/>. Its lines are kept.
/// </summary>
/// <example>
/// <code>
/// &lt;MentionText Text="@comment.Body" /&gt;
/// &lt;MentionText Text="@note" Prefixes="Triggers"
///              Href="@(m => $"/people/{m.Text}")" /&gt;
/// </code>
/// </example>
/// <remarks>Mentions are found as <see cref="Mention.Parse"/> finds them.</remarks>
/// <accessibility>
/// <list type="bullet">
/// <item>A mention without a link is text in a different color and weight, read as part of the sentence.</item>
/// <item>With <see cref="Href"/>, each mention is a link named by its text, such as "@ana".</item>
/// </list>
/// </accessibility>
/// <seealso cref="Mentions{TItem}"/>
/// <seealso cref="Comment"/>
[ModeSupport]
public class MentionText : LayoutItemBase
{
    /// <summary>The text to show.</summary>
    [Parameter, EditorRequired] public string? Text { get; set; }

    /// <summary>The characters that start a mention. Defaults to <c>@</c>.</summary>
    [Parameter] public IReadOnlyList<string>? Prefixes { get; set; }

    /// <summary>Where a mention links to, such as the person's profile; null for no link.</summary>
    [Parameter] public Func<Mention, string?>? Href { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-mention-text");
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cls, style) = BuildRootAttributes();
        var text = Text ?? "";
        builder.OpenElement(0, "link");
        builder.AddAttribute(1, "rel", "stylesheet");
        builder.AddAttribute(2, "href", Assets["_content/IzzyBlazor.Core/mentions.css"]);
        builder.CloseElement();
        builder.OpenElement(3, "span");
        builder.AddMultipleAttributes(4, AdditionalAttributes);
        builder.AddAttribute(5, "class", cls);
        builder.AddAttribute(6, "style", style);
        var at = 0;
        foreach (var mention in Mention.Parse(text, [.. Prefixes ?? []]))
        {
            builder.AddContent(7, text[at..mention.Index]);
            var href = Href?.Invoke(mention);
            builder.OpenElement(8, href is null ? "span" : "a");
            builder.AddAttribute(9, "class", "ib-mention");
            builder.AddAttribute(10, "href", href);
            builder.AddContent(11, mention.Prefix + mention.Text);
            builder.CloseElement();
            at = mention.Index + mention.Length;
        }

        builder.AddContent(12, text[at..]);
        builder.CloseElement();
    }
}
