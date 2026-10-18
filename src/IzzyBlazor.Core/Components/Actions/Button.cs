using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace IzzyBlazor;

/// <summary>
/// A native <c>button</c>. On static pages it submits or resets forms like plain HTML; on
/// interactive pages <see cref="OnClick"/> runs C#.
/// </summary>
/// <example>
/// <code>
/// &lt;Button Submit&gt;Save&lt;/Button&gt;
/// &lt;Button Accent="Accent.Danger" Variant="ButtonVariant.Outline" StartIcon="Lucide.Trash"&gt;Delete&lt;/Button&gt;
/// &lt;Button Submit Name="action" Value="publish"&gt;Publish&lt;/Button&gt;
/// </code>
/// </example>
public class Button : ButtonBase
{
    /// <summary>
    /// Submits the form it's in (or <see cref="Form"/>), like <c>type="submit"</c>. Without
    /// <c>Submit</c> or <see cref="Reset"/>, a button does nothing by itself.
    /// </summary>
    [Parameter] public bool Submit { get; set; }

    /// <summary>Resets the form it's in, like <c>type="reset"</c>. Can't be combined with <see cref="Submit"/>.</summary>
    [Parameter] public bool Reset { get; set; }

    /// <summary>Replaced by <see cref="Submit"/> and <see cref="Reset"/>. Kept for one release so old markup fails to build.</summary>
    [Parameter, Obsolete("Buttons take a bare Submit or Reset now: <Button Submit>. Type and ButtonType go in a later release.", error: true)]
    public ButtonType Type { get; set; }

    /// <summary>
    /// Posted with the form as <c>Name=Value</c> when this button submits it, so one handler can tell
    /// which of several submit buttons was pressed.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>The value posted with <see cref="Name"/>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Submits to this URL instead of the form's own action (native <c>formaction</c>).</summary>
    [Parameter] public string? FormAction { get; set; }

    /// <summary>The id of a form elsewhere on the page that this button submits (native <c>form</c>).</summary>
    [Parameter] public string? Form { get; set; }

    /// <summary>Runs on click on interactive pages. Has no effect on static pages.</summary>
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>
    /// Attributes a derived button adds, such as <see cref="DialogTrigger"/>'s <c>commandfor</c>.
    /// Written before <see cref="IzzyComponentBase.AdditionalAttributes"/>, so the caller can still override them.
    /// </summary>
    protected virtual IEnumerable<KeyValuePair<string, object>>? ExtraAttributes => null;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();

        var type = ButtonTypes.Of(Submit, Reset, GetType().Name);
        builder.OpenElement(0, "button");
        // While loading, a submit button can't submit again (Enter in a field, Space on the button).
        builder.AddAttribute(1, "type", Loading ? "button" : type);
        builder.AddAttribute(2, "name", Name);
        builder.AddAttribute(3, "value", Value);
        builder.AddAttribute(4, "formaction", FormAction);
        builder.AddAttribute(5, "form", Form);
        builder.AddAttribute(6, "disabled", Disabled);
        if (Loading)
        {
            // Not "disabled": the button keeps focus while busy. CSS blocks clicks.
            builder.AddAttribute(7, "aria-disabled", "true");
            builder.AddAttribute(8, "aria-busy", "true");
        }

        if (OnClick.HasDelegate && !Loading)
        {
            builder.AddAttribute(9, "onclick", OnClick);
        }

        builder.AddMultipleAttributes(10, ExtraAttributes);
        builder.AddMultipleAttributes(11, AdditionalAttributes);
        builder.AddAttribute(12, "class", cssClass);
        builder.AddAttribute(13, "style", style);
        builder.OpenRegion(14);
        RenderContent(builder);
        builder.CloseRegion();
        builder.CloseElement();
    }
}

/// <summary>The <c>type</c> attribute for a button's <c>Submit</c> and <c>Reset</c> parameters.</summary>
internal static class ButtonTypes
{
    public static string Of(bool submit, bool reset, string component) => (submit, reset) switch
    {
        (true, true) => throw new InvalidOperationException($"{component} can't set both Submit and Reset."),
        (true, _) => "submit",
        (_, true) => "reset",
        _ => "button",
    };
}

/// <summary>Replaced by the <c>Submit</c> and <c>Reset</c> parameters. Kept for one release so old markup fails to build.</summary>
[Obsolete("Buttons take a bare Submit or Reset now: <Button Submit>. Type and ButtonType go in a later release.", error: true)]
public enum ButtonType
{
    /// <summary>Replaced: a button does nothing by itself without <c>Submit</c> or <c>Reset</c>.</summary>
    Button,

    /// <summary>Replaced by <c>Submit</c>.</summary>
    Submit,

    /// <summary>Replaced by <c>Reset</c>.</summary>
    Reset,
}
