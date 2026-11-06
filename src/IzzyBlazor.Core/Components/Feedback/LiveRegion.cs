using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// Hidden text that screen readers read when it changes, such as a filtered list's "12 results".
/// For text that shows on the page, put <c>role="status"</c> on the element that shows it instead.
/// For something that just happened ("Row deleted"), use <see cref="Announcer"/>.
/// </summary>
/// <example><code>&lt;LiveRegion Text="@($"{orders.Count} orders")" /&gt;</code></example>
/// <accessibility>
/// <list type="bullet">
/// <item>Polite (<c>role="status"</c>): read when the screen reader is done with what it's saying. <see cref="Assertive"/> (<c>role="alert"</c>) interrupts; keep it for errors that stop the task.</item>
/// <item>It's read when its text changes. The same text again isn't read: it describes the page, and nothing changed.</item>
/// <item>Put it on the page from the start. A polite region's first text isn't read: screen readers only watch regions that were already there. An assertive one is read when it appears with text, so give it text only when there's an error to report.</item>
/// <item>Set <see cref="Assertive"/> once: screen readers don't reliably notice a region's role changing.</item>
/// <item>The whole text is read each time (<c>aria-atomic</c>), not just the part that changed.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Announcer"/>
/// <seealso cref="Alert"/>
[ModeSupport(NoScript = ModeLevel.Partial, NoScriptNotes = "With JavaScript off, the text changes only with a new page, and text a page loads with isn't read.")]
public class LiveRegion : IzzyComponentBase
{
    /// <summary>What's read when it changes.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>Interrupts what the screen reader is saying (<c>role="alert"</c>). Defaults to false (polite).</summary>
    [Parameter] public bool Assertive { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-visually-hidden");
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cls, style) = BuildRootAttributes();
        builder.OpenElement(0, "div");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cls);
        builder.AddAttribute(3, "style", style);
        // The role alone: role="alert" with aria-live as well is read twice by VoiceOver on iOS.
        builder.AddAttribute(4, "role", Assertive ? "alert" : "status");
        builder.AddAttribute(5, "aria-atomic", "true");
        builder.AddContent(6, Text);
        builder.CloseElement();
    }
}
