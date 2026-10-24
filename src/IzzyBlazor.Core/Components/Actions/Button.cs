using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace IzzyBlazor;

/// <summary>
/// A native <c>button</c>, or a link (<c>a</c>) that looks like one when <see cref="Href"/> is set.
/// On static pages a button submits or resets forms like plain HTML; on interactive pages
/// <see cref="OnClick"/> runs C#. Use a link for navigation ("Start the project") and a button
/// for actions.
/// </summary>
/// <example>
/// <code>
/// &lt;Button Submit&gt;Save&lt;/Button&gt;
/// &lt;Button Accent="Accent.Danger" Variant="ButtonVariant.Outline" StartIcon="Lucide.Trash"&gt;Delete&lt;/Button&gt;
/// &lt;Button Submit Name="intent" Value="publish"&gt;Publish&lt;/Button&gt;
/// &lt;Button Href="/courses/wizard" EndIcon="Lucide.ArrowRight"&gt;Start the project&lt;/Button&gt;
/// &lt;Button Href="https://github.com/..." External Variant="ButtonVariant.Outline"&gt;Get the repo&lt;/Button&gt;
/// </code>
/// </example>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders a native <c>button</c> or <c>a</c>, so Enter, Space and focus work as the browser defines them.</item>
/// <item>The focus ring shows for keyboard focus only.</item>
/// <item>An action shown only as an icon needs a name: use <see cref="IconButton"/>, whose <c>Label</c> is required.</item>
/// <item>A loading button keeps focus and sets <c>aria-busy</c>; screen readers hear <see cref="ButtonBase.LoadingLabel"/>.</item>
/// <item>A disabled or loading link loses its <c>href</c>, keeps <c>role="link"</c> and sets <c>aria-disabled</c>.</item>
/// <item>An external link says it opens a new tab (<see cref="NewTabLabel"/>), not just with its icon.</item>
/// </list>
/// </accessibility>
[ModeSupport(Notes = "OnClick runs only on interactive pages; Submit, Reset and Href work everywhere.")]
public class Button : ButtonBase
{
    /// <summary>
    /// Makes it a link to this URL (an <c>a</c>) instead of a button. A link can't take
    /// <see cref="Submit"/>, <see cref="Reset"/>, <see cref="Name"/>, <see cref="Value"/>,
    /// <see cref="FormAction"/> or <see cref="Form"/>.
    /// </summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>
    /// For a link: opens in a new tab with <c>rel="noopener noreferrer"</c> and shows an
    /// external-link icon.
    /// </summary>
    [Parameter] public bool External { get; set; }

    /// <summary>
    /// Leaves out an external link's icon, for repeated links where it would be noise. Screen
    /// readers still hear <see cref="NewTabLabel"/>.
    /// </summary>
    [Parameter] public bool HideExternalIcon { get; set; }

    /// <summary>Read to screen readers after an external link's text. Defaults to "(opens in a new tab)".</summary>
    [Parameter] public string? NewTabLabel { get; set; }

    /// <summary>
    /// Submits the form it's in (or <see cref="Form"/>), like <c>type="submit"</c>. Without
    /// <c>Submit</c> or <see cref="Reset"/>, a button does nothing by itself.
    /// </summary>
    [Parameter] public bool Submit { get; set; }

    /// <summary>Resets the form it's in, like <c>type="reset"</c>. Can't be combined with <see cref="Submit"/>.</summary>
    [Parameter] public bool Reset { get; set; }

    /// <summary>
    /// Posted with the form as <c>Name=Value</c> when this button submits it, so one handler can tell
    /// which of several submit buttons was pressed. Not <c>action</c>, <c>method</c> or <c>target</c>:
    /// a control with one of those names hides the form's own property, and Blazor's enhanced form
    /// handling then posts to the wrong address.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>The value posted with <see cref="Name"/>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Submits to this URL instead of the form's own action (native <c>formaction</c>).</summary>
    [Parameter] public string? FormAction { get; set; }

    /// <summary>The id of a form elsewhere on the page that this button submits (native <c>form</c>).</summary>
    [Parameter] public string? Form { get; set; }

    /// <summary>Runs on click on interactive pages (a link still navigates). Has no effect on static pages.</summary>
    [Parameter, ModeSupport(Off = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>
    /// Attributes a derived button adds, such as <see cref="DialogTrigger"/>'s <c>commandfor</c>.
    /// Written before <see cref="IzzyComponentBase.AdditionalAttributes"/>, so the caller can still override them.
    /// </summary>
    protected virtual IEnumerable<KeyValuePair<string, object>>? ExtraAttributes => null;

    /// <summary>Whether this kind of button can be a link. Dialog buttons can't.</summary>
    private protected virtual bool CanBeLink => true;

    /// <inheritdoc />
    private protected override bool WrapsInToolbar => true;

    /// <inheritdoc />
    private protected override bool ExternalIcon => Href is not null && External && !HideExternalIcon;

    /// <inheritdoc />
    private protected override RenderFragment? ToolbarMenuItem(string? text) =>
        // A derived button (a dialog's trigger) or one wired up by its attributes does more than a menu
        // item can, and a menu item can't reset a form.
        GetType() != typeof(Button) || Reset || !CopiesToMenu ? null : builder =>
        {
            builder.OpenComponent<MenuItem>(0);
            builder.AddComponentParameter(1, nameof(MenuItem.Href), Href);
            builder.AddComponentParameter(2, nameof(MenuItem.Submit), Submit);
            builder.AddComponentParameter(3, nameof(MenuItem.Name), Name);
            builder.AddComponentParameter(4, nameof(MenuItem.Value), Value);
            builder.AddComponentParameter(5, nameof(MenuItem.FormAction), FormAction);
            builder.AddComponentParameter(6, nameof(MenuItem.Form), Form);
            builder.AddComponentParameter(7, nameof(MenuItem.Icon), StartIcon);
            builder.AddComponentParameter(8, nameof(MenuItem.Disabled), Disabled || Loading);
            builder.AddComponentParameter(9, nameof(MenuItem.Accent), EffectiveAccent == Accent.Danger ? Accent.Danger : Accent.Neutral);
            if (OnClick.HasDelegate)
            {
                builder.AddComponentParameter(10, nameof(MenuItem.OnClick), OnClick);
            }

            if (External && Href is not null)
            {
                builder.AddAttribute(11, "target", "_blank");
                builder.AddAttribute(12, "rel", "noopener noreferrer");
            }

            builder.AddComponentParameter(13, nameof(MenuItem.ChildContent), text is not null ? (RenderFragment)(b => b.AddContent(0, text)) : ChildContent);
            builder.CloseComponent();
        };

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ToolbarPart.Wrap(RenderButton));

    private void RenderButton(RenderTreeBuilder builder)
    {
        if (Href is not null)
        {
            RenderLink(builder);
            return;
        }

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

    private void RenderLink(RenderTreeBuilder builder)
    {
        var component = GetType().Name;
        if (!CanBeLink)
        {
            throw new InvalidOperationException($"{component} can't take Href: it's a button. Use a Button with Href for a link.");
        }

        if (Submit || Reset || Name is not null || Value is not null || FormAction is not null || Form is not null)
        {
            throw new InvalidOperationException(
                $"{component} with Href is a link, so Submit, Reset, Name, Value, FormAction and Form don't apply. Leave out Href for a button.");
        }

        var (cssClass, style) = BuildRootAttributes();
        var inactive = Disabled || Loading;

        builder.OpenElement(20, "a");
        if (inactive)
        {
            // A link with no href can't be followed or reached with Tab; role="link" keeps its name.
            builder.AddAttribute(21, "role", "link");
            builder.AddAttribute(22, "aria-disabled", "true");
        }
        else
        {
            builder.AddAttribute(23, "href", Href);
        }

        if (Loading)
        {
            builder.AddAttribute(24, "aria-busy", "true");
        }

        if (External)
        {
            builder.AddAttribute(25, "target", "_blank");
            builder.AddAttribute(26, "rel", "noopener noreferrer");
        }

        if (OnClick.HasDelegate && !inactive)
        {
            builder.AddAttribute(27, "onclick", OnClick);
        }

        builder.AddMultipleAttributes(28, ExtraAttributes);
        builder.AddMultipleAttributes(29, AdditionalAttributes);
        builder.AddAttribute(30, "class", cssClass);
        builder.AddAttribute(31, "style", style);
        builder.OpenRegion(32);
        RenderContent(builder);
        builder.CloseRegion();

        if (External)
        {
            if (EndIcon is null && !HideExternalIcon)
            {
                builder.OpenComponent<Icon>(33);
                builder.AddComponentParameter(34, nameof(Icon.Role), (IconRole?)IconRole.ExternalLink);
                builder.AddComponentParameter(35, nameof(Icon.Size), IconSize.Sm);
                builder.CloseComponent();
            }

            builder.OpenElement(36, "span");
            builder.AddAttribute(37, "class", "ib-visually-hidden");
            builder.AddContent(38, $" {NewTabLabel ?? TextDefaults.OpensInNewTab}");
            builder.CloseElement();
        }

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
