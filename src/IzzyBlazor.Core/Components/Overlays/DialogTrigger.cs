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
/// &lt;Footer&gt;
///     &lt;DialogClose&gt;Cancel&lt;/DialogClose&gt;
///     &lt;Button Submit Form="invite-form"&gt;Send invite&lt;/Button&gt;
/// &lt;/Footer&gt;
/// </code>
/// </example>
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

    private string Target => For ?? Dialog?.Id
        ?? throw new InvalidOperationException("DialogClose must be inside a Dialog, or set For to the dialog's id.");

    // Inside another form there's no close form to submit, and submitting would send the outer form.
    private bool UsesCloseForm => Dialog?.HasCloseForm ?? true;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // It submits its close form; Reset (or no close form) makes it an ordinary button instead.
        if (!UsesCloseForm || Reset)
        {
            Submit = false;
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object>>? ExtraAttributes => UsesCloseForm
        ?
        [
            new("form", $"{Target}-close"),
            new("commandfor", Target),
            new("command", "close"),
        ]
        :
        [
            new("commandfor", Target),
            new("command", "close"),
        ];
}
