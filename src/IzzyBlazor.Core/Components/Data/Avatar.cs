using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>Avatar sizes.</summary>
public enum AvatarSize
{
    /// <summary>1.5rem, for dense lists and inline mentions.</summary>
    Xs,

    /// <summary>2rem, beside a line of text.</summary>
    Sm,

    /// <summary>2.5rem (the default).</summary>
    Md,

    /// <summary>3rem.</summary>
    Lg,

    /// <summary>4rem.</summary>
    Xl,

    /// <summary>6rem, for a profile header.</summary>
    Xl2,
}

/// <summary>
/// A picture of a person or thing: an image, else initials from <see cref="Name"/>, else an icon.
/// When the image fails to load, the initials show through, with no script needed.
/// </summary>
/// <example>
/// <code>
/// &lt;Avatar Name="Grace Hopper" Src="/img/grace.jpg" /&gt;
/// &lt;Avatar Name="Ada Lovelace" Size="AvatarSize.Lg" Status="Status.Success" StatusLabel="Online" /&gt;
/// &lt;Avatar Name="Acme Corp" Square Color="Color.Primary" /&gt;
/// </code>
/// </example>
public class Avatar : IzzyComponentBase, IDisposable
{
    // Soft hues for initials, picked from the name so a person keeps the same color everywhere.
    private static readonly int[] Hues = [25, 55, 95, 150, 190, 230, 270, 320];

    /// <summary>
    /// The person's or thing's name: the accessible name, and the source of the initials and of
    /// their color.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>The image URL. Without one, or if it fails to load, the initials show.</summary>
    [Parameter] public string? Src { get; set; }

    /// <summary>Text in place of initials taken from <see cref="Name"/>, such as "GH" or "+".</summary>
    [Parameter] public string? Initials { get; set; }

    /// <summary>The icon when there's no name. Defaults to a person.</summary>
    [Parameter] public IconData? Icon { get; set; }

    /// <summary>Size. Defaults to <see cref="AvatarSize.Md"/>, or the group's size inside an <see cref="AvatarGroup"/>.</summary>
    [Parameter] public AvatarSize? Size { get; set; }

    /// <summary>Rounded corners in place of a circle, for teams, companies and projects.</summary>
    [Parameter] public bool Square { get; set; }

    /// <summary>The color behind the initials. Defaults to a soft color picked from <see cref="Name"/>.</summary>
    [Parameter] public Color? Color { get; set; }

    /// <summary>A dot in the corner, such as <see cref="IzzyBlazor.Status.Success"/> for online. Give it a <see cref="StatusLabel"/>.</summary>
    [Parameter] public Status? Status { get; set; }

    /// <summary>What the status dot means, for screen readers, such as "Online".</summary>
    [Parameter] public string? StatusLabel { get; set; }

    /// <summary>
    /// Hides the avatar from screen readers, for when the name is already written beside it.
    /// </summary>
    [Parameter] public bool Decorative { get; set; }

    [CascadingParameter] private AvatarGroup? Group { get; set; }

    private AvatarSize ResolvedSize => Size ?? Group?.Size ?? AvatarSize.Md;

    /// <summary>The initials shown for a name: the first letters of its first and last words.</summary>
    internal static string InitialsOf(string? name)
    {
        var words = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => char.IsLetterOrDigit(w[0]))
            .ToArray();
        return words.Length switch
        {
            0 => "",
            1 => FirstLetter(words[0]),
            _ => FirstLetter(words[0]) + FirstLetter(words[^1]),
        };

        static string FirstLetter(string word) =>
            char.IsSurrogate(word[0]) && word.Length > 1 ? word[..2] : char.ToUpperInvariant(word[0]).ToString();
    }

    /// <summary>A URL as a quoted CSS <c>url()</c>, escaped so it can't end the string early.</summary>
    internal static string CssUrl(string url)
    {
        var escaped = new System.Text.StringBuilder(url.Length + 8);
        foreach (var c in url)
        {
            switch (c)
            {
                case '\\' or '"':
                    escaped.Append('\\').Append(c);
                    break;
                case < ' ' or '\u007f':
                    escaped.Append('\\').Append(((int)c).ToString("x", System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
                    break;
                default:
                    escaped.Append(c);
                    break;
            }
        }

        return $"url(\"{escaped}\")";
    }

    /// <summary>A stable hue for a name (the same on every server and every run).</summary>
    internal static int HueOf(string name)
    {
        // FNV-1a: string.GetHashCode is randomized per process.
        var hash = 2166136261u;
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            hash = (hash ^ c) * 16777619u;
        }

        return Hues[hash % (uint)Hues.Length];
    }

    /// <inheritdoc />
    protected override void OnInitialized() => Group?.Register(this);

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-avatar");
        classes.Add($"ib-avatar-{ResolvedSize.ToString().ToLowerInvariant()}", ResolvedSize != AvatarSize.Md);
        classes.Add("ib-avatar-square", Square);
        if (Color is { } color)
        {
            classes.Add("ib-avatar-colored");
            ColorOverride.Apply(styles, color);
        }
        else if (string.IsNullOrWhiteSpace(Name) && Initials is null)
        {
            classes.Add("ib-avatar-anonymous");
        }
        else
        {
            styles.AddVar("avatar-hue", HueOf(Name ?? Initials ?? "").ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cls, style) = BuildRootAttributes();
        var initials = Initials ?? InitialsOf(Name);
        var label = Name is null ? null : StatusLabel is null || Status is null ? Name : $"{Name} ({StatusLabel})";

        builder.OpenElement(0, "span");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cls);
        builder.AddAttribute(3, "style", style);
        if (Decorative || label is null)
        {
            builder.AddAttribute(4, "aria-hidden", "true");
        }
        else
        {
            builder.AddAttribute(5, "role", "img");
            builder.AddAttribute(6, "aria-label", label);
        }

        // The fallback sits under the image, so a broken image (empty alt, so nothing drawn)
        // shows it without any script.
        builder.OpenElement(7, "span");
        builder.AddAttribute(8, "class", "ib-avatar-fallback");
        if (initials.Length > 0)
        {
            builder.AddContent(9, initials);
        }
        else
        {
            builder.OpenComponent<Icon>(10);
            builder.AddComponentParameter(11, nameof(IzzyBlazor.Icon.Name), Icon ?? Lucide.User);
            builder.CloseComponent();
        }

        builder.CloseElement();

        // The photo is a background over the fallback: if it fails to load, nothing is drawn and
        // the initials show, in every browser and with no script (an <img> shows a broken icon).
        if (!string.IsNullOrEmpty(Src))
        {
            builder.OpenElement(12, "span");
            builder.AddAttribute(13, "class", "ib-avatar-img");
            builder.AddAttribute(14, "style", $"background-image:{CssUrl(Src)}");
            builder.CloseElement();
        }

        if (Status is { } status)
        {
            builder.OpenElement(18, "span");
            builder.AddAttribute(19, "class", $"ib-avatar-status ib-avatar-status-{status.ToString().ToLowerInvariant()}");
            builder.AddAttribute(20, "aria-hidden", "true");
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    /// <inheritdoc />
    public void Dispose() => Group?.Unregister(this);
}
