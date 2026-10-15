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
    /// <summary>The bundled Hugeicons mapping (Stroke Rounded).</summary>
    public static IconSet Hugeicons { get; } = new(role => role switch
    {
        IconRole.Close => IzzyBlazor.Hugeicons.Cancel01,
        IconRole.ChevronDown => IzzyBlazor.Hugeicons.ArrowDown01,
        IconRole.ChevronUp => IzzyBlazor.Hugeicons.ArrowUp01,
        IconRole.ChevronLeft => IzzyBlazor.Hugeicons.ArrowLeft01,
        IconRole.ChevronRight => IzzyBlazor.Hugeicons.ArrowRight01,
        IconRole.Check => IzzyBlazor.Hugeicons.Tick02,
        IconRole.Minus => IzzyBlazor.Hugeicons.MinusSign,
        IconRole.Plus => IzzyBlazor.Hugeicons.Add01,
        IconRole.Menu => IzzyBlazor.Hugeicons.Menu01,
        IconRole.Search => IzzyBlazor.Hugeicons.Search01,
        IconRole.MoreHorizontal => IzzyBlazor.Hugeicons.MoreHorizontal,
        IconRole.Info => IzzyBlazor.Hugeicons.InformationCircle,
        IconRole.Success => IzzyBlazor.Hugeicons.CheckmarkCircle02,
        IconRole.Warning => IzzyBlazor.Hugeicons.Alert02,
        IconRole.Error => IzzyBlazor.Hugeicons.AlertCircle,
        IconRole.ExternalLink => IzzyBlazor.Hugeicons.LinkSquare02,
        IconRole.Copy => IzzyBlazor.Hugeicons.Copy01,
        IconRole.Copied => IzzyBlazor.Hugeicons.CopyCheck,
        IconRole.Calendar => IzzyBlazor.Hugeicons.Calendar03,
        IconRole.Clock => IzzyBlazor.Hugeicons.Clock01,
        IconRole.Show => IzzyBlazor.Hugeicons.View,
        IconRole.Hide => IzzyBlazor.Hugeicons.ViewOff,
        IconRole.Loading => IzzyBlazor.Hugeicons.Loading03,
        IconRole.SortAscending => IzzyBlazor.Hugeicons.ArrowUp02,
        IconRole.SortDescending => IzzyBlazor.Hugeicons.ArrowDown02,
        IconRole.Sortable => IzzyBlazor.Hugeicons.ArrowUpDown,
        IconRole.TrendUp => IzzyBlazor.Hugeicons.ArrowUpRight01,
        IconRole.TrendDown => IzzyBlazor.Hugeicons.ArrowDownRight01,
        IconRole.TrendFlat => IzzyBlazor.Hugeicons.ArrowRight02,
        IconRole.Filter => IzzyBlazor.Hugeicons.Filter,
        IconRole.SchemeLight => IzzyBlazor.Hugeicons.Sun03,
        IconRole.SchemeDark => IzzyBlazor.Hugeicons.Moon02,
        IconRole.SchemeSystem => IzzyBlazor.Hugeicons.Computer,
        IconRole.Person => IzzyBlazor.Hugeicons.User,
        IconRole.Play => IzzyBlazor.Hugeicons.Play,
        IconRole.Upload => IzzyBlazor.Hugeicons.CloudUpload,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    });

    /// <summary>
    /// The set IzzyBlazor components use. Hugeicons for now; becomes configurable when more
    /// icon sets are supported.
    /// </summary>
    public static IconSet Current => Hugeicons;

    private readonly Func<IconRole, IconData> _resolve;

    /// <summary>Creates an icon set from a role-to-icon mapping. Internal until other sets are supported.</summary>
    internal IconSet(Func<IconRole, IconData> resolve) => _resolve = resolve;

    /// <summary>The icon for a role.</summary>
    public IconData Get(IconRole role) => _resolve(role);
}
