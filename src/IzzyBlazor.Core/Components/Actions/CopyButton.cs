using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A button that copies text: an install command, an ID, a key. It copies <see cref="Text"/>, or
/// the value or text of the element <see cref="For"/> names, and shows a check and "Copied" for a
/// moment, like a <see cref="Code"/> block's copy button.
/// </summary>
/// <example>
/// <code>
/// &lt;Code&gt;dotnet add package IzzyBlazor&lt;/Code&gt;
/// &lt;CopyButton Text="dotnet add package IzzyBlazor" Label="Copy the command" /&gt;
///
/// &lt;TextField id="api-key" Label="API key" Value="@key" ReadOnly&gt;
///     &lt;EndAddon&gt;&lt;CopyButton For="api-key" Variant="ButtonVariant.Outline"&gt;Copy&lt;/CopyButton&gt;&lt;/EndAddon&gt;
/// &lt;/TextField&gt;
/// </code>
/// </example>
/// <remarks>
/// Copying needs JavaScript (and a secure page), so the button appears only once the script runs;
/// with it off, the text is still there to select. A ghost neutral icon button by default; with
/// content it shows the text after the icon.
/// </remarks>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders a native <c>button</c>, shown only once the script runs; with JavaScript off the text is still there to select.</item>
/// <item>Without content it's named by <see cref="Label"/> ("Copy"); with content it's named by the content, and <see cref="Label"/> is only the tooltip.</item>
/// <item>After copying, an icon-only button's name becomes <see cref="CopiedLabel"/> ("Copied") for a moment; a button with text keeps its name, and "Copied" is announced politely.</item>
/// <item>If the page has no clipboard access, the button stays as it was and nothing is announced.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Code"/>
/// <seealso cref="IconButton"/>
/// <seealso cref="TextField"/>
[ModeSupport(Off = ModeLevel.None, Notes = "Copying needs JavaScript: with it off, the button doesn't appear.")]
public class CopyButton : ButtonBase
{
    /// <summary>Creates the button: neutral and ghost unless set.</summary>
    public CopyButton()
    {
        Accent = Accent.Neutral;
        Variant = ButtonVariant.Ghost;
    }

    /// <summary>The text to copy.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>
    /// The id of the element to copy from, in place of <see cref="Text"/>: an input's or text
    /// area's value, or any other element's text.
    /// </summary>
    [Parameter] public string? For { get; set; }

    /// <summary>
    /// The button's name for screen readers and its tooltip. Defaults to "Copy". A button with
    /// content is named by its content and uses this only as the tooltip.
    /// </summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Announced after copying. Defaults to "Copied".</summary>
    [Parameter] public string? CopiedLabel { get; set; }

    /// <inheritdoc />
    protected override bool IconOnly => ChildContent is null;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Text is null == For is null)
        {
            throw new InvalidOperationException("CopyButton copies either Text or the element named by For: set one of them.");
        }

        if (StartIcon is not null || EndIcon is not null || Loading || LoadingLabel is not null)
        {
            throw new InvalidOperationException("CopyButton draws its own copy and check icons: StartIcon, EndIcon, Loading and LoadingLabel don't apply to it.");
        }
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-copy-btn");
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        var label = Label ?? TextDefaults.Copy;

        builder.OpenElement(0, "ib-copy");
        builder.AddAttribute(1, "data-copy-text", Text);
        builder.AddAttribute(2, "data-copy-for", For);
        builder.OpenElement(3, "button");
        builder.AddAttribute(4, "type", "button");
        builder.AddAttribute(5, "disabled", Disabled);
        builder.AddAttribute(6, "aria-label", IconOnly ? label : null);
        builder.AddAttribute(7, "title", label);
        builder.AddAttribute(8, "data-copied-label", CopiedLabel ?? TextDefaults.Copied);
        builder.AddMultipleAttributes(9, AdditionalAttributes);
        builder.AddAttribute(10, "class", cssClass);
        builder.AddAttribute(11, "style", style);

        builder.OpenComponent<Icon>(12);
        builder.AddComponentParameter(13, nameof(Icon.Role), (IconRole?)IconRole.Copy);
        builder.AddComponentParameter(14, nameof(Icon.Class), "ib-copy-icon");
        builder.CloseComponent();
        builder.OpenComponent<Icon>(15);
        builder.AddComponentParameter(16, nameof(Icon.Role), (IconRole?)IconRole.Copied);
        builder.AddComponentParameter(17, nameof(Icon.Class), "ib-copied-icon");
        builder.CloseComponent();
        if (ChildContent is not null)
        {
            builder.OpenElement(18, "span");
            builder.AddAttribute(19, "class", "ib-btn-label");
            builder.AddContent(20, ChildContent);
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    }
}
