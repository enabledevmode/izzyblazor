namespace IzzyBlazor;

/// <summary>
/// The icons IzzyBlazor's own components use, by purpose rather than by name, so the
/// whole library can switch icon sets in one place.
/// </summary>
public enum IconRole
{
    /// <summary>Close a dialog, toast or alert.</summary>
    Close,

    /// <summary>Open a menu or expand downward.</summary>
    ChevronDown,

    /// <summary>Collapse upward.</summary>
    ChevronUp,

    /// <summary>Previous, or back.</summary>
    ChevronLeft,

    /// <summary>Next, or forward.</summary>
    ChevronRight,

    /// <summary>Selected or done.</summary>
    Check,

    /// <summary>Remove, decrease, or indeterminate.</summary>
    Minus,

    /// <summary>Add or increase.</summary>
    Plus,

    /// <summary>Open the navigation menu.</summary>
    Menu,

    /// <summary>Search.</summary>
    Search,

    /// <summary>More actions.</summary>
    MoreHorizontal,

    /// <summary>Informational message.</summary>
    Info,

    /// <summary>Success message.</summary>
    Success,

    /// <summary>Warning message.</summary>
    Warning,

    /// <summary>Error message.</summary>
    Error,

    /// <summary>Opens in a new tab.</summary>
    ExternalLink,

    /// <summary>Copy to clipboard.</summary>
    Copy,

    /// <summary>Copied.</summary>
    Copied,

    /// <summary>Date input.</summary>
    Calendar,

    /// <summary>Time input.</summary>
    Clock,

    /// <summary>Show password.</summary>
    Show,

    /// <summary>Hide password.</summary>
    Hide,

    /// <summary>Busy or loading (spun with CSS).</summary>
    Loading,

    /// <summary>Column sorted ascending.</summary>
    SortAscending,

    /// <summary>Column sorted descending.</summary>
    SortDescending,

    /// <summary>Column sortable, not sorted.</summary>
    Sortable,

    /// <summary>A figure went up.</summary>
    TrendUp,

    /// <summary>A figure went down.</summary>
    TrendDown,

    /// <summary>A figure didn't change.</summary>
    TrendFlat,

    /// <summary>Filter a column or list.</summary>
    Filter,

    /// <summary>The light color scheme.</summary>
    SchemeLight,

    /// <summary>The dark color scheme.</summary>
    SchemeDark,

    /// <summary>Follow the device's color scheme.</summary>
    SchemeSystem,

    /// <summary>A person with no photo or name, such as an empty avatar.</summary>
    Person,

    /// <summary>Play a video.</summary>
    Play,

    /// <summary>Upload a file.</summary>
    Upload,
}

/// <summary>
/// Maps each <see cref="IconRole"/> to an icon. IzzyBlazor components ask the icon set for a
/// role instead of naming an icon, so a different set can be swapped in later.
/// </summary>
public class IconSet
{
    /// <summary>The bundled Tabler mapping (outline icons).</summary>
    public static IconSet Tabler { get; } = new(role => role switch
    {
        IconRole.Close => IzzyBlazor.Tabler.X,
        IconRole.ChevronDown => IzzyBlazor.Tabler.ChevronDown,
        IconRole.ChevronUp => IzzyBlazor.Tabler.ChevronUp,
        IconRole.ChevronLeft => IzzyBlazor.Tabler.ChevronLeft,
        IconRole.ChevronRight => IzzyBlazor.Tabler.ChevronRight,
        IconRole.Check => IzzyBlazor.Tabler.Check,
        IconRole.Minus => IzzyBlazor.Tabler.Minus,
        IconRole.Plus => IzzyBlazor.Tabler.Plus,
        IconRole.Menu => IzzyBlazor.Tabler.Menu2,
        IconRole.Search => IzzyBlazor.Tabler.Search,
        IconRole.MoreHorizontal => IzzyBlazor.Tabler.Dots,
        IconRole.Info => IzzyBlazor.Tabler.InfoCircle,
        IconRole.Success => IzzyBlazor.Tabler.CircleCheck,
        IconRole.Warning => IzzyBlazor.Tabler.AlertTriangle,
        IconRole.Error => IzzyBlazor.Tabler.AlertCircle,
        IconRole.ExternalLink => IzzyBlazor.Tabler.ExternalLink,
        IconRole.Copy => IzzyBlazor.Tabler.Copy,
        IconRole.Copied => IzzyBlazor.Tabler.CopyCheck,
        IconRole.Calendar => IzzyBlazor.Tabler.Calendar,
        IconRole.Clock => IzzyBlazor.Tabler.Clock,
        IconRole.Show => IzzyBlazor.Tabler.Eye,
        IconRole.Hide => IzzyBlazor.Tabler.EyeOff,
        IconRole.Loading => IzzyBlazor.Tabler.Loader2,
        IconRole.SortAscending => IzzyBlazor.Tabler.ArrowUp,
        IconRole.SortDescending => IzzyBlazor.Tabler.ArrowDown,
        IconRole.Sortable => IzzyBlazor.Tabler.ArrowsSort,
        IconRole.TrendUp => IzzyBlazor.Tabler.ArrowUpRight,
        IconRole.TrendDown => IzzyBlazor.Tabler.ArrowDownRight,
        IconRole.TrendFlat => IzzyBlazor.Tabler.ArrowRight,
        IconRole.Filter => IzzyBlazor.Tabler.Filter,
        IconRole.SchemeLight => IzzyBlazor.Tabler.Sun,
        IconRole.SchemeDark => IzzyBlazor.Tabler.Moon,
        IconRole.SchemeSystem => IzzyBlazor.Tabler.DeviceDesktop,
        IconRole.Person => IzzyBlazor.Tabler.User,
        IconRole.Play => IzzyBlazor.Tabler.PlayerPlay,
        IconRole.Upload => IzzyBlazor.Tabler.CloudUpload,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    });

    /// <summary>
    /// The set IzzyBlazor components use. Tabler for now; becomes configurable when more
    /// icon sets are supported.
    /// </summary>
    public static IconSet Current => Tabler;

    private readonly Func<IconRole, IconData> _resolve;

    /// <summary>Creates an icon set from a role-to-icon mapping. Internal until other sets are supported.</summary>
    internal IconSet(Func<IconRole, IconData> resolve) => _resolve = resolve;

    /// <summary>The icon for a role.</summary>
    public IconData Get(IconRole role) => _resolve(role);
}
