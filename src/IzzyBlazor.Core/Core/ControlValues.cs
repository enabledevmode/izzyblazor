namespace IzzyBlazor;

/// <summary>
/// The color of an action: buttons, icon buttons, menu items. Emphasis comes from the
/// variant (solid, soft, outline, ghost, link), not from more colors. For anything else,
/// such as a brand color for one button, pass a <see cref="Color"/> instead.
/// </summary>
public enum Accent
{
    /// <summary>The brand color, for the main action.</summary>
    Primary,

    /// <summary>A quiet, gray action.</summary>
    Neutral,

    /// <summary>A destructive action, such as Delete.</summary>
    Danger,
}

/// <summary>
/// The meaning of a piece of feedback: badges, and later alerts and toasts. Status colors
/// are for telling people what happened, not for actions; see <see cref="Accent"/>.
/// </summary>
public enum Status
{
    /// <summary>No particular meaning (the default).</summary>
    Neutral,

    /// <summary>Information, such as "New" or "Beta".</summary>
    Info,

    /// <summary>Done or healthy, such as "Published".</summary>
    Success,

    /// <summary>Needs attention, such as "In review" or "Expiring".</summary>
    Warning,

    /// <summary>Failed or blocked, such as "Error" or "Overdue".</summary>
    Danger,
}

/// <summary>A button's visual weight, from strongest to quietest.</summary>
public enum ButtonVariant
{
    /// <summary>Filled with the accent color. For the main action.</summary>
    Solid,

    /// <summary>A light tint of the accent color.</summary>
    Soft,

    /// <summary>An accent-colored border on a transparent background.</summary>
    Outline,

    /// <summary>Transparent until hovered. For toolbars and dense layouts.</summary>
    Ghost,

    /// <summary>Looks like a link.</summary>
    Link,
}

/// <summary>
/// The height of interactive controls. Shared by buttons and form fields, so a button
/// and an input of the same size line up in a row.
/// </summary>
public enum ControlSize
{
    /// <summary>32px tall, small text.</summary>
    Sm,

    /// <summary>36px tall (the default).</summary>
    Md,

    /// <summary>44px tall, large text. For touch-first and marketing pages.</summary>
    Lg,
}

/// <summary>What a button does inside a form.</summary>
public enum ButtonType
{
    /// <summary>Nothing by itself (the default). Use for actions handled in C# or by script.</summary>
    Button,

    /// <summary>Submits the form it is in.</summary>
    Submit,

    /// <summary>Resets the form it is in.</summary>
    Reset,
}
