using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>Shared settings for the columns of a <see cref="Table{TItem}"/> or <c>DataGrid</c>.</summary>
public abstract class ColumnBase<TItem> : ComponentBase, IDisposable
{
    /// <summary>The header text.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Lets people sort by this column, with a link in its header.</summary>
    [Parameter] public bool Sortable { get; set; }

    /// <summary>
    /// The name of this column in the URL's sort parameter (<c>?sort=total</c>, or
    /// <c>?sort=-total</c> for descending) and its filter (<c>?f-total=</c>). Defaults to the
    /// property name in lower case.
    /// </summary>
    [Parameter] public string? SortKey { get; set; }

    /// <summary>
    /// Adds a filter button to this column's header: a text box for text (matching anywhere,
    /// ignoring case), Min and Max boxes for numbers (keeping the values between them; either may
    /// be empty), a date input for dates (matching the day), a menu for enums and booleans. A
    /// <see cref="TemplateColumn{TItem}"/> filters by its <c>FilterBy</c> text.
    /// </summary>
    [Parameter] public bool Filterable { get; set; }

    /// <summary>
    /// For an enum filter: choose several values from a menu of checkboxes, and keep the items
    /// that have any of them. The URL repeats the key (<c>?f-status=Paid&amp;f-status=Pending</c>).
    /// </summary>
    [Parameter] public bool FilterMultiple { get; set; }

    /// <summary>
    /// Builds this column's filter condition yourself, for a computed value or one stored
    /// differently from how it's shown. It gets the condition (operator and values parsed for the
    /// column's type) and returns the filter on the item, or null for the usual one. See
    /// <see cref="DataField{TItem}.Filter"/>.
    /// </summary>
    [Parameter] public Func<FieldFilter, Expression<Func<TItem, bool>>?>? Filter { get; set; }

    /// <summary>How the cells line up. Defaults to the end for numbers and the start otherwise.</summary>
    [Parameter] public CellAlign? Align { get; set; }

    /// <summary>The column width, such as <c>Length.Rem(8)</c>. In a grid, a fixed length: not a percent or a keyword.</summary>
    [Parameter] public Length? Width { get; set; }

    /// <summary>
    /// How narrow a column without a <see cref="Width"/> can get before the grid scrolls sideways.
    /// Defaults to 8rem. Not for a Table, whose columns size to their content.
    /// </summary>
    [Parameter] public Length? MinWidth { get; set; }

    /// <summary>
    /// Keeps this column in view at the grid's start or end while the rest scrolls sideways, and draws
    /// it at that edge wherever it's declared. It needs a <see cref="Width"/>. Not for a Table.
    /// </summary>
    [Parameter] public ColumnFrozen Frozen { get; set; }

    /// <summary>A class for this column's cells and header.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Text for this column in the table's footer row, such as a total.</summary>
    [Parameter] public string? FooterText { get; set; }

    /// <summary>Content for this column in the table's footer row, in place of <see cref="FooterText"/>.</summary>
    [Parameter] public RenderFragment? Footer { get; set; }

    internal bool HasFooter => Footer is not null || FooterText is not null;

    internal RenderFragment FooterCell => Footer ?? (b => b.AddContent(0, FooterText));

    [CascadingParameter] internal TableViewBase<TItem>? Table { get; set; }

    internal string HeaderText => Title ?? DefaultTitle ?? "";

    internal string? Key => SortKey ?? DefaultSortKey;

    internal bool CanSort => Sortable && Key is not null && HasSortExpression;

    internal bool CanFilter => Filterable && Key is not null && FilterKind != ColumnFilterKind.None;

    /// <summary>What kind of filter the column offers.</summary>
    internal virtual ColumnFilterKind FilterKind => ColumnFilterKind.None;

    /// <summary>The options of a select filter: value and text.</summary>
    internal virtual IReadOnlyList<(string Value, string Text)> FilterChoices(IzzyText text) => [];

    /// <summary>The type of the value the column filters, or null when it can't.</summary>
    internal virtual Type? ValueType => null;

    /// <summary>The column as a field, for requests: null when it has no key or nothing to read.</summary>
    internal abstract DataField<TItem>? ToField(IzzyText text);

    /// <summary>Why <see cref="Filterable"/> can't work on this column, or null when it can.</summary>
    protected virtual string? FilterProblem => null;

    internal CellAlign EffectiveAlign => Align ?? DefaultAlign;

    /// <summary>A title worked out from the column's property, if any.</summary>
    protected virtual string? DefaultTitle => null;

    /// <summary>A sort key worked out from the column's property, if any.</summary>
    protected virtual string? DefaultSortKey => null;

    /// <summary>The alignment when <see cref="Align"/> isn't set.</summary>
    protected virtual CellAlign DefaultAlign => CellAlign.Start;

    /// <summary>Whether the column knows what to sort by.</summary>
    protected abstract bool HasSortExpression { get; }

    /// <summary>The cell content for an item.</summary>
    internal abstract RenderFragment Cell(TItem item);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Table is null)
        {
            throw new InvalidOperationException($"{GetType().Name} must be inside a Table or a DataGrid.");
        }

        Table.AddColumn(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (MinWidth is not null && Table is { GridColumns: false })
        {
            throw new InvalidOperationException($"The \"{HeaderText}\" column has a {nameof(MinWidth)}, which only a DataGrid's columns take.");
        }

        if (Frozen != ColumnFrozen.None && Table is { GridColumns: false })
        {
            throw new InvalidOperationException($"The \"{HeaderText}\" column is {nameof(Frozen)}, which only a DataGrid's columns can be.");
        }

        if (Table is { GridColumns: true })
        {
            if (Frozen != ColumnFrozen.None && Width is null)
            {
                throw new InvalidOperationException($"The \"{HeaderText}\" column is {nameof(Frozen)}, so it needs a {nameof(Width)}: the columns after it are placed by adding the widths up.");
            }

            // The grid adds its widths up, and a percent of the table or a keyword can't be added.
            foreach (var (name, length) in new[] { (nameof(Width), Width), (nameof(MinWidth), MinWidth) })
            {
                if (length?.ToString() is { } css && (css.EndsWith('%') || css is "auto" or "fit-content" or "min-content" or "max-content"))
                {
                    throw new InvalidOperationException($"The \"{HeaderText}\" column's {name} is {css}: a DataGrid's column widths are fixed lengths, such as Length.Rem(8). Leave {nameof(Width)} out to share the rest of the space.");
                }
            }

            if (Key?.Contains(',') == true)
            {
                throw new InvalidOperationException($"The \"{HeaderText}\" column's {nameof(SortKey)} is \"{Key}\": a DataGrid's sort lists keys with commas, so a key can't hold one.");
            }
        }

        if (!Filterable)
        {
            return;
        }

        if (FilterProblem is { } problem)
        {
            throw new InvalidOperationException(problem);
        }

        if (Key is null)
        {
            throw new InvalidOperationException($"The \"{HeaderText}\" column needs a {nameof(SortKey)} to name its filter.");
        }
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => false;

    private object? _shown;

    /// <summary>What the table shows for this column, apart from cell templates: a change re-renders the table.</summary>
    protected virtual object Shown => (Title, Sortable, SortKey, Align, Width, MinWidth, Frozen, Class, FooterText, Footer is not null, Filterable, FilterMultiple);

    /// <inheritdoc />
    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters);
        var shown = Shown;
        if (_shown is not null && !Equals(_shown, shown))
        {
            Table?.ColumnChanged();
        }

        _shown = shown;
    }

    /// <inheritdoc />
    public void Dispose() => Table?.RemoveColumn(this);

    internal static bool IsNumeric(Type type) => FilterValues.IsNumeric(type);
}

/// <summary>Where a <c>DataGrid</c> column stays in view while the rest scrolls sideways.</summary>
public enum ColumnFrozen
{
    /// <summary>It scrolls with the rest. The default.</summary>
    None,

    /// <summary>At the grid's start (the left, or the right in a right-to-left page).</summary>
    Start,

    /// <summary>At the grid's end.</summary>
    End,
}

/// <summary>
/// A column that shows one property of each item, formatted, and can sort by it. The title and
/// sort key come from the property ([Display(Name)] is used when present).
/// </summary>
/// <example><code>&lt;PropertyColumn Property="o =&gt; o.Total" Format="C" Sortable /&gt;</code></example>
public class PropertyColumn<TItem, TValue> : ColumnBase<TItem>
{
    private Expression<Func<TItem, TValue>>? _lastProperty;
    private Func<TItem, TValue>? _compiled;
    private MemberInfo? _member;
    private string? _path;

    /// <summary>The property to show, such as <c>o =&gt; o.Customer.Name</c>.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, TValue>> Property { get; set; } = default!;

    /// <summary>A .NET format string for the value, such as <c>C</c>, <c>N0</c> or <c>yyyy-MM-dd</c>.</summary>
    [Parameter] public string? Format { get; set; }

    /// <inheritdoc />
    protected override object Shown => (base.Shown, Format);

    /// <inheritdoc />
    protected override string? DefaultTitle =>
        _member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? (_member is null ? null : DataFields.Words(_member.Name));

    /// <inheritdoc />
    protected override string? DefaultSortKey => _path;

    /// <inheritdoc />
    protected override CellAlign DefaultAlign => IsNumeric(typeof(TValue)) ? CellAlign.End : CellAlign.Start;

    /// <inheritdoc />
    protected override bool HasSortExpression => true;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Property is null)
        {
            throw new InvalidOperationException($"{nameof(PropertyColumn<TItem, TValue>)} needs a {nameof(Property)}.");
        }

        if (!ReferenceEquals(Property, _lastProperty))
        {
            _lastProperty = Property;
            _compiled = Property.Compile();
            var members = new List<string>();
            _member = null;
            for (var body = Property.Body; body is MemberExpression member; body = member.Expression!)
            {
                _member ??= member.Member;
                members.Insert(0, member.Member.Name.ToLowerInvariant());
            }

            _path = members.Count > 0 ? string.Join('.', members) : null;
        }

        base.OnParametersSet();
    }

    /// <inheritdoc />
    internal override ColumnFilterKind FilterKind => ColumnFilter.KindOf(typeof(TValue));

    /// <inheritdoc />
    protected override string? FilterProblem => FilterKind == ColumnFilterKind.None
        ? $"The \"{HeaderText}\" column can't filter {typeof(TValue).Name} values: Filterable works with text, numbers, dates, enums and booleans. Use a TemplateColumn with FilterBy for anything else."
        : null;

    /// <inheritdoc />
    internal override IReadOnlyList<(string Value, string Text)> FilterChoices(IzzyText text) => ColumnFilter.Choices(typeof(TValue), text);

    /// <inheritdoc />
    internal override Type? ValueType => typeof(TValue);

    /// <inheritdoc />
    internal override DataField<TItem>? ToField(IzzyText text) => Key is not { } key || Property is null ? null : new()
    {
        Key = key,
        Value = Property,
        Caption = HeaderText,
        Format = Format,
        Sortable = CanSort,
        Filterable = CanFilter,
        Searchable = FilterKind == ColumnFilterKind.Text,
        Choices = FilterKind == ColumnFilterKind.Choice ? FilterChoices(text) : null,
        Filter = Filter,
    };

    internal override RenderFragment Cell(TItem item) => builder =>
    {
        var value = _compiled!(item);
        builder.AddContent(0, Format is not null && value is IFormattable formattable
            ? formattable.ToString(Format, CultureInfo.CurrentCulture)
            : value?.ToString());
    };
}

/// <summary>
/// A column with any content per item, such as a badge or buttons. Give it <see cref="SortBy"/>
/// to make it sortable.
/// </summary>
/// <example><code>&lt;TemplateColumn Title="Status" SortBy="o =&gt; o.Status" Sortable&gt;&lt;Badge&gt;@context.Status&lt;/Badge&gt;&lt;/TemplateColumn&gt;</code></example>
public class TemplateColumn<TItem> : ColumnBase<TItem>
{
    /// <summary>The cell content; <c>context</c> is the item.</summary>
    [Parameter] public RenderFragment<TItem>? ChildContent { get; set; }

    /// <summary>What to sort by when <see cref="ColumnBase{TItem}.Sortable"/>. Needs a <see cref="ColumnBase{TItem}.SortKey"/>.</summary>
    [Parameter] public Expression<Func<TItem, object?>>? SortBy { get; set; }

    /// <summary>
    /// The text to filter by when <see cref="ColumnBase{TItem}.Filterable"/>, such as
    /// <c>o =&gt; o.Customer.Name</c>. It matches anywhere, ignoring case. Needs a
    /// <see cref="ColumnBase{TItem}.SortKey"/>.
    /// </summary>
    [Parameter] public Expression<Func<TItem, string?>>? FilterBy { get; set; }

    /// <inheritdoc />
    protected override bool HasSortExpression => SortBy is not null;

    /// <inheritdoc />
    internal override ColumnFilterKind FilterKind => FilterBy is null ? ColumnFilterKind.None : ColumnFilterKind.Text;

    /// <inheritdoc />
    protected override string? FilterProblem => FilterBy is null
        ? $"The \"{HeaderText}\" column needs {nameof(FilterBy)} to be Filterable."
        : null;

    /// <inheritdoc />
    internal override Type? ValueType => FilterBy is null ? null : typeof(string);

    /// <inheritdoc />
    internal override DataField<TItem>? ToField(IzzyText text) => Key is not { } key || (FilterBy ?? (LambdaExpression?)SortBy) is not { } value ? null : new()
    {
        Key = key,
        Value = value,
        SortValue = FilterBy is null ? null : SortBy,
        Caption = HeaderText,
        Sortable = CanSort,
        Filterable = CanFilter,
        Searchable = FilterBy is not null,
        Filter = Filter,
    };

    internal override RenderFragment Cell(TItem item) => ChildContent?.Invoke(item) ?? (_ => { });
}

/// <summary>Tells a table its columns have registered. Rendered after the columns, so its
/// initialization runs once they have; static rendering waits for the returned task.</summary>
internal sealed class TableColumnsReady : ComponentBase
{
    [Parameter] public Func<Task>? Ready { get; set; }

    protected override Task OnInitializedAsync() => Ready?.Invoke() ?? Task.CompletedTask;

    protected override bool ShouldRender() => false;
}
