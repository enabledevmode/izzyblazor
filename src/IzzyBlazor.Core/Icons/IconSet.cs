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

    /// <summary>Play a video.</summary>
    Play,

    /// <summary>Upload a file.</summary>
    Upload,

    /// <summary>Back to the first page.</summary>
    FirstPage,

    /// <summary>Open a side panel.</summary>
    Panel,

    /// <summary>A filter for text that contains the value.</summary>
    FilterContains,

    /// <summary>A filter for text that doesn't contain the value.</summary>
    FilterDoesNotContain,

    /// <summary>A filter for what equals the value.</summary>
    FilterEquals,

    /// <summary>A filter for what doesn't equal the value.</summary>
    FilterNotEquals,

    /// <summary>A filter for text that starts with the value.</summary>
    FilterStartsWith,

    /// <summary>A filter for text that ends with the value.</summary>
    FilterEndsWith,

    /// <summary>A filter for what is greater than the value.</summary>
    FilterGreaterThan,

    /// <summary>A filter for what is at least the value.</summary>
    FilterGreaterOrEqual,

    /// <summary>A filter for what is less than the value.</summary>
    FilterLessThan,

    /// <summary>A filter for what is at most the value.</summary>
    FilterLessOrEqual,

    /// <summary>A filter for what is between two values.</summary>
    FilterBetween,

    /// <summary>A filter for what is empty.</summary>
    FilterIsEmpty,

    /// <summary>A filter for what isn't empty.</summary>
    FilterIsNotEmpty,

    /// <summary>Put something back as it was.</summary>
    Reset,
}

/// <summary>
/// Maps each <see cref="IconRole"/> to an icon. IzzyBlazor components ask the icon set for a
/// role instead of naming an icon, so a different set can be swapped in later.
/// </summary>
public class IconSet
{
    /// <summary>The bundled Lucide mapping, with Tabler's for the filter operators Lucide has none for.</summary>
    public static IconSet Lucide { get; } = new(role => role switch
    {
        IconRole.Close => IzzyBlazor.Lucide.X,
        IconRole.ChevronDown => IzzyBlazor.Lucide.ChevronDown,
        IconRole.ChevronUp => IzzyBlazor.Lucide.ChevronUp,
        IconRole.ChevronLeft => IzzyBlazor.Lucide.ChevronLeft,
        IconRole.ChevronRight => IzzyBlazor.Lucide.ChevronRight,
        IconRole.Check => IzzyBlazor.Lucide.Check,
        IconRole.Minus => IzzyBlazor.Lucide.Minus,
        IconRole.Plus => IzzyBlazor.Lucide.Plus,
        IconRole.Menu => IzzyBlazor.Lucide.Menu,
        IconRole.Search => IzzyBlazor.Lucide.Search,
        IconRole.MoreHorizontal => IzzyBlazor.Lucide.Ellipsis,
        IconRole.MoreVertical => IzzyBlazor.Lucide.EllipsisVertical,
        IconRole.Export => IzzyBlazor.Lucide.Download,
        IconRole.Columns => IzzyBlazor.Lucide.Columns3,
        IconRole.Info => IzzyBlazor.Lucide.Info,
        IconRole.Success => IzzyBlazor.Lucide.CircleCheck,
        IconRole.Warning => IzzyBlazor.Lucide.TriangleAlert,
        IconRole.Error => IzzyBlazor.Lucide.CircleAlert,
        IconRole.ExternalLink => IzzyBlazor.Lucide.ExternalLink,
        IconRole.Copy => IzzyBlazor.Lucide.Copy,
        IconRole.Copied => IzzyBlazor.Lucide.CopyCheck,
        IconRole.Calendar => IzzyBlazor.Lucide.Calendar,
        IconRole.Clock => IzzyBlazor.Lucide.Clock,
        IconRole.Show => IzzyBlazor.Lucide.Eye,
        IconRole.Hide => IzzyBlazor.Lucide.EyeOff,
        IconRole.Loading => IzzyBlazor.Lucide.LoaderCircle,
        IconRole.SortAscending => IzzyBlazor.Lucide.ArrowUp,
        IconRole.SortDescending => IzzyBlazor.Lucide.ArrowDown,
        IconRole.Sortable => IzzyBlazor.Lucide.ArrowUpDown,
        IconRole.TrendUp => IzzyBlazor.Lucide.ArrowUpRight,
        IconRole.TrendDown => IzzyBlazor.Lucide.ArrowDownRight,
        IconRole.TrendFlat => IzzyBlazor.Lucide.ArrowRight,
        IconRole.Filter => IzzyBlazor.Lucide.ListFilter,
        IconRole.SchemeLight => IzzyBlazor.Lucide.Sun,
        IconRole.SchemeDark => IzzyBlazor.Lucide.Moon,
        IconRole.SchemeSystem => IzzyBlazor.Lucide.Monitor,
        IconRole.Person => IzzyBlazor.Lucide.User,
        IconRole.Play => IzzyBlazor.Lucide.Play,
        IconRole.Upload => IzzyBlazor.Lucide.Upload,
        IconRole.FirstPage => IzzyBlazor.Lucide.ChevronsLeft,
        IconRole.Panel => IzzyBlazor.Lucide.PanelRight,
        IconRole.FilterContains => OperatorIcons.Contains,
        IconRole.FilterDoesNotContain => OperatorIcons.DoesNotContain,
        IconRole.FilterEquals => IzzyBlazor.Lucide.Equal,
        IconRole.FilterNotEquals => IzzyBlazor.Lucide.EqualNot,
        IconRole.FilterStartsWith => OperatorIcons.StartsWith,
        IconRole.FilterEndsWith => OperatorIcons.EndsWith,
        IconRole.FilterGreaterThan => OperatorIcons.GreaterThan,
        IconRole.FilterGreaterOrEqual => OperatorIcons.GreaterOrEqual,
        IconRole.FilterLessThan => OperatorIcons.LessThan,
        IconRole.FilterLessOrEqual => OperatorIcons.LessOrEqual,
        IconRole.FilterBetween => IzzyBlazor.Lucide.MoveHorizontal,
        IconRole.FilterIsEmpty => IzzyBlazor.Lucide.CircleDashed,
        IconRole.FilterIsNotEmpty => IzzyBlazor.Lucide.CircleDot,
        IconRole.Reset => IzzyBlazor.Lucide.RotateCcw,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    });

    /// <summary>
    /// The set IzzyBlazor components use. Lucide for now; becomes configurable when more
    /// icon sets are supported.
    /// </summary>
    public static IconSet Current => Lucide;

    private readonly Func<IconRole, IconData> _resolve;

    /// <summary>Creates an icon set from a role-to-icon mapping. Internal until other sets are supported.</summary>
    internal IconSet(Func<IconRole, IconData> resolve) => _resolve = resolve;

    /// <summary>The icon for a role.</summary>
    public IconData Get(IconRole role) => _resolve(role);
}
