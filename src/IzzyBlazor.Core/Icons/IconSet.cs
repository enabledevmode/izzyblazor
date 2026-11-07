namespace IzzyBlazor;

/// <summary>
/// The icons IzzyBlazor's own components use, by purpose rather than by name, so the
/// whole library can switch icon sets in one place.
/// </summary>
public enum IconPurpose
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

    /// <summary>A DataGrid header's menu button.</summary>
    MoreVertical,

    /// <summary>A DataGrid's export.</summary>
    Export,

    /// <summary>A DataGrid's column picker.</summary>
    Columns,

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

    /// <summary>Play a video, or start a carousel's rotation.</summary>
    Play,

    /// <summary>Stop a carousel's rotation.</summary>
    Pause,

    /// <summary>Upload a file.</summary>
    Upload,

    /// <summary>Back to the first page.</summary>
    FirstPage,

    /// <summary>Open a side panel.</summary>
    Panel,

    /// <summary>From one end of a range to the other.</summary>
    RangeTo,
}

/// <summary>
/// Maps each <see cref="IconPurpose"/> to an icon. IzzyBlazor components ask the icon set for a
/// role instead of naming an icon, so a different set can be swapped in later.
/// </summary>
public class IconSet
{
    /// <summary>The bundled Lucide mapping.</summary>
    public static IconSet Lucide { get; } = new(role => role switch
    {
        IconPurpose.Close => IzzyBlazor.Lucide.X,
        IconPurpose.ChevronDown => IzzyBlazor.Lucide.ChevronDown,
        IconPurpose.ChevronUp => IzzyBlazor.Lucide.ChevronUp,
        IconPurpose.ChevronLeft => IzzyBlazor.Lucide.ChevronLeft,
        IconPurpose.ChevronRight => IzzyBlazor.Lucide.ChevronRight,
        IconPurpose.Check => IzzyBlazor.Lucide.Check,
        IconPurpose.Minus => IzzyBlazor.Lucide.Minus,
        IconPurpose.Plus => IzzyBlazor.Lucide.Plus,
        IconPurpose.Menu => IzzyBlazor.Lucide.Menu,
        IconPurpose.Search => IzzyBlazor.Lucide.Search,
        IconPurpose.MoreHorizontal => IzzyBlazor.Lucide.Ellipsis,
        IconPurpose.MoreVertical => IzzyBlazor.Lucide.EllipsisVertical,
        IconPurpose.Export => IzzyBlazor.Lucide.Download,
        IconPurpose.Columns => IzzyBlazor.Lucide.Columns3,
        IconPurpose.Info => IzzyBlazor.Lucide.Info,
        IconPurpose.Success => IzzyBlazor.Lucide.CircleCheck,
        IconPurpose.Warning => IzzyBlazor.Lucide.TriangleAlert,
        IconPurpose.Error => IzzyBlazor.Lucide.CircleAlert,
        IconPurpose.ExternalLink => IzzyBlazor.Lucide.ExternalLink,
        IconPurpose.Copy => IzzyBlazor.Lucide.Copy,
        IconPurpose.Copied => IzzyBlazor.Lucide.CopyCheck,
        IconPurpose.Calendar => IzzyBlazor.Lucide.Calendar,
        IconPurpose.Clock => IzzyBlazor.Lucide.Clock,
        IconPurpose.Show => IzzyBlazor.Lucide.Eye,
        IconPurpose.Hide => IzzyBlazor.Lucide.EyeOff,
        IconPurpose.Loading => IzzyBlazor.Lucide.LoaderCircle,
        IconPurpose.SortAscending => IzzyBlazor.Lucide.ArrowUp,
        IconPurpose.SortDescending => IzzyBlazor.Lucide.ArrowDown,
        IconPurpose.Sortable => IzzyBlazor.Lucide.ArrowUpDown,
        IconPurpose.TrendUp => IzzyBlazor.Lucide.ArrowUpRight,
        IconPurpose.TrendDown => IzzyBlazor.Lucide.ArrowDownRight,
        IconPurpose.TrendFlat => IzzyBlazor.Lucide.ArrowRight,
        IconPurpose.Filter => OperatorIcons.Filter,
        IconPurpose.SchemeLight => IzzyBlazor.Lucide.Sun,
        IconPurpose.SchemeDark => IzzyBlazor.Lucide.Moon,
        IconPurpose.SchemeSystem => IzzyBlazor.Lucide.Monitor,
        IconPurpose.Person => IzzyBlazor.Lucide.User,
        IconPurpose.Play => IzzyBlazor.Lucide.Play,
        IconPurpose.Pause => IzzyBlazor.Lucide.Pause,
        IconPurpose.Upload => IzzyBlazor.Lucide.Upload,
        IconPurpose.FirstPage => IzzyBlazor.Lucide.ChevronsLeft,
        IconPurpose.Panel => IzzyBlazor.Lucide.PanelRight,
        IconPurpose.RangeTo => IzzyBlazor.Lucide.ArrowRight,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    });

    /// <summary>
    /// The set IzzyBlazor components use. Lucide for now; becomes configurable when more
    /// icon sets are supported.
    /// </summary>
    public static IconSet Current => Lucide;

    private readonly Func<IconPurpose, IconData> _resolve;

    /// <summary>Creates an icon set from a role-to-icon mapping. Internal until other sets are supported.</summary>
    internal IconSet(Func<IconPurpose, IconData> resolve) => _resolve = resolve;

    /// <summary>The icon for a role.</summary>
    public IconData Get(IconPurpose role) => _resolve(role);
}
