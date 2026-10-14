namespace IzzyBlazor;

/// <summary>
/// The color of a control: buttons, badges, tags, banners, progress bars and menu items.
/// <see cref="Primary"/>, <see cref="Neutral"/> and <see cref="Danger"/> are the roles; emphasis
/// comes from the variant (solid, soft, outline, ghost, link), not from more colors. For one
/// control in another color, use <see cref="Of"/> (any CSS color) or <see cref="Token"/> (a theme
/// token); text on it is chosen for contrast.
/// </summary>
/// <example>
/// <code>
/// &lt;Button Accent="Accent.Danger"&gt;Delete&lt;/Button&gt;
/// &lt;Button Accent='Accent.Of("#0d9488")'&gt;Custom&lt;/Button&gt;
/// &lt;Badge Accent='Accent.Token("brand")'&gt;New&lt;/Badge&gt;
/// </code>
/// </example>
public sealed class Accent : IEquatable<Accent>
{
    private Accent(string? role, Color? custom)
    {
        Role = role;
        Custom = custom;
    }

    /// <summary>The brand color, for the main action.</summary>
    public static Accent Primary { get; } = new("primary", null);

    /// <summary>A quiet, gray action.</summary>
    public static Accent Neutral { get; } = new("neutral", null);

    /// <summary>A destructive action, such as Delete.</summary>
    public static Accent Danger { get; } = new("danger", null);

    /// <summary>Any CSS color, such as <c>"#0d9488"</c>. Text on it is black or white, whichever reads better.</summary>
    public static Accent Of(string css) => new(null, Color.Of(css));

    /// <summary>A theme token, such as <c>"brand"</c> for <c>--ib-color-brand</c>; text on it uses <c>--ib-color-on-brand</c> when the theme defines it.</summary>
    public static Accent Token(string name) => new(null, Color.Token(name));

    /// <summary>A <see cref="IzzyBlazor.Color"/> as an accent, such as <c>Color.Success</c>.</summary>
    public static Accent From(Color color) => new(null, color);

    /// <summary>The role's name (<c>primary</c>, <c>neutral</c>, <c>danger</c>), or null for a custom color.</summary>
    internal string? Role { get; }

    /// <summary>The custom color, or null for a role.</summary>
    internal Color? Custom { get; }

    /// <summary>The color to paint with: the role's token, or the custom color.</summary>
    internal Color ToColor() => Custom ?? Color.Token(Role!);

    /// <inheritdoc />
    public bool Equals(Accent? other) =>
        other is not null && Role == other.Role && Custom?.Css == other.Custom?.Css;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Accent);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Role, Custom?.Css);

    /// <summary>Whether two accents are the same role or color.</summary>
    public static bool operator ==(Accent? left, Accent? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Whether two accents differ.</summary>
    public static bool operator !=(Accent? left, Accent? right) => !(left == right);

    /// <inheritdoc />
    public override string ToString() => Role ?? Custom!.Value.Css;
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

/// <summary>Where a <see cref="NumberInput{TValue}"/> puts its increase and decrease buttons.</summary>
public enum StepButtons
{
    /// <summary>Stacked at the end of the field (the default).</summary>
    Stacked,

    /// <summary>Decrease at the start and increase at the end, full height: easier to tap.</summary>
    Split,

    /// <summary>No buttons; arrow keys still step.</summary>
    None,
}
