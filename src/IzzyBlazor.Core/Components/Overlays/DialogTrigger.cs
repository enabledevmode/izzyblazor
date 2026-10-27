using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// A <see cref="Button"/> that opens a <see cref="Dialog"/> by id. It uses the native
/// <c>commandfor</c> and <c>command="show-modal"</c> attributes, so it needs no C# and, in
/// current browsers, no script; IzzyBlazor's script fills in for older ones.
/// </summary>
/// <example>
/// <code>
/// &lt;DialogTrigger For="invite"&gt;Invite people&lt;/DialogTrigger&gt;
/// </code>
/// </example>
public class DialogTrigger : Button
{
    /// <summary>The id of the dialog to open.</summary>
    [Parameter, EditorRequired] public string For { get; set; } = "";

    private protected override bool CanBeLink => false;

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object>>? ExtraAttributes =>
    [
        new("commandfor", For),
        new("command", "show-modal"),
        new("aria-haspopup", "dialog"),
    ];

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(For))
        {
            throw new InvalidOperationException("DialogTrigger needs For: the id of the dialog to open.");
        }
    }
}

/// <summary>
/// A <see cref="Button"/> that closes the <see cref="Dialog"/> it is in. It submits the dialog's
/// <c>method="dialog"</c> form, which closes it natively in every browser, script or not. In a
/// dialog inside another form, it uses the native <c>close</c> command instead.
/// Defaults to a neutral outline button, for "Cancel".
/// </summary>
/// <example>
/// <code>
/// &lt;DialogFooter&gt;
///     &lt;DialogClose&gt;Cancel&lt;/DialogClose&gt;
///     &lt;Button Submit Form="invite-form"&gt;Send invite&lt;/Button&gt;
/// &lt;/DialogFooter&gt;
/// </code>
/// </example>
[RefusesParameter(nameof(Reset))]
public class DialogClose : Button
{
    /// <summary>Creates a close button with neutral outline styling.</summary>
    public DialogClose()
    {
        Accent = Accent.Neutral;
        Variant = ButtonVariant.Outline;
        Submit = true;
    }

    /// <summary>The id of the dialog to close. Defaults to the dialog this button is in.</summary>
    [Parameter] public string? For { get; set; }

    [CascadingParameter] private DialogContext? Dialog { get; set; }

    private protected override bool CanBeLink => false;

    private string Target => For ?? Dialog?.Id
        ?? throw new InvalidOperationException("DialogClose must be inside a Dialog, or set For to the dialog's id.");

    // Inside another form there's no close form to submit, and submitting would send the outer form.
    private bool UsesCloseForm => Dialog?.HasCloseForm ?? true;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // Reset comes from Button, but a reset button can't close a dialog: browsers ignore the
        // close command on one, and the close form has nothing to reset.
        if (parameters.TryGetValue<bool>(nameof(Reset), out _))
        {
            throw new InvalidOperationException(
                "DialogClose closes the dialog, so it doesn't take Reset. To reset a form as well, put a Button with Reset beside it.");
        }

        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // It submits its close form; with no close form it's an ordinary button instead.
        if (!UsesCloseForm)
        {
            Submit = false;
        }
    }

    // Submitting the close form closes it. Otherwise (no close form, or Submit set to false) it's
    // an ordinary button with the native close command, which browsers ignore on a submit button.
    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object>>? ExtraAttributes => Submit
        ?
        [
            new("form", $"{Target}-close"),
        ]
        :
        [
            new("commandfor", Target),
            new("command", "close"),
        ];
}
