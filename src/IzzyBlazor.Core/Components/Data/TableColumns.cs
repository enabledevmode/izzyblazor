using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>Shared settings for <see cref="Table{TItem}"/> columns.</summary>
public abstract class ColumnBase<TItem> : ComponentBase, IDisposable
{
    /// <summary>The header text.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Lets people sort by this column, with a link in its header.</summary>
    [Parameter] public bool Sortable { get; set; }

    /// <summary>
    /// The name of this column in the URL's sort parameter (<c>?sort=total</c>, or
    /// <c>?sort=-total</c> for descending). Defaults to the property name in lower case.
    /// </summary>
    [Parameter] public string? SortKey { get; set; }

    /// <summary>How the cells line up. Defaults to the end for numbers and the start otherwise.</summary>
    [Parameter] public CellAlign? Align { get; set; }

    /// <summary>The column width, such as <c>Length.Rem(8)</c>.</summary>
    [Parameter] public Length? Width { get; set; }

    /// <summary>A class for this column's cells and header.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Text for this column in the table's footer row, such as a total.</summary>
    [Parameter] public string? FooterText { get; set; }

    /// <summary>Content for this column in the table's footer row, in place of <see cref="FooterText"/>.</summary>
    [Parameter] public RenderFragment? Footer { get; set; }

    internal bool HasFooter => Footer is not null || FooterText is not null;

    internal RenderFragment FooterCell => Footer ?? (b => b.AddContent(0, FooterText));

    [CascadingParameter] internal Table<TItem>? Table { get; set; }

    internal string HeaderText => Title ?? DefaultTitle ?? "";

    internal string? Key => SortKey ?? DefaultSortKey;

    internal bool CanSort => Sortable && Key is not null && HasSortExpression;

    internal CellAlign EffectiveAlign => Align ?? DefaultAlign;

    /// <summary>A title worked out from the column's property, if any.</summary>
    protected virtual string? DefaultTitle => null;

    /// <summary>A sort key worked out from the column's property, if any.</summary>
    protected virtual string? DefaultSortKey => null;

    /// <summary>The alignment when <see cref="Align"/> isn't set.</summary>
    protected virtual CellAlign DefaultAlign => CellAlign.Start;

    /// <summary>Whether the column knows what to sort by.</summary>
    protected abstract bool HasSortExpression { get; }

    /// <summary>Orders the query by this column.</summary>
    internal abstract IQueryable<TItem> ApplySort(IQueryable<TItem> source, bool descending);

    /// <summary>The cell content for an item.</summary>
    internal abstract RenderFragment Cell(TItem item);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Table is null)
        {
            throw new InvalidOperationException($"{GetType().Name} must be inside a Table.");
        }

        Table.AddColumn(this);
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => false;

    private object? _shown;

    /// <summary>What the table shows for this column, apart from cell templates: a change re-renders the table.</summary>
    protected virtual object Shown => (Title, Sortable, SortKey, Align, Width, Class, FooterText, Footer is not null);

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

    internal static bool IsNumeric(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(int) || type == typeof(long) || type == typeof(decimal) || type == typeof(double)
            || type == typeof(float) || type == typeof(short) || type == typeof(byte) || type == typeof(uint)
            || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);
    }
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
        _member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? (_member is null ? null : Words(_member.Name));

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
    }

    internal override IQueryable<TItem> ApplySort(IQueryable<TItem> source, bool descending) =>
        descending ? source.OrderByDescending(Property) : source.OrderBy(Property);

    internal override RenderFragment Cell(TItem item) => builder =>
    {
        var value = _compiled!(item);
        builder.AddContent(0, Format is not null && value is IFormattable formattable
            ? formattable.ToString(Format, CultureInfo.CurrentCulture)
            : value?.ToString());
    };

    // "OrderDate" -> "Order date"
    private static string Words(string name)
    {
        var chars = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                chars.Append(' ').Append(char.ToLowerInvariant(c));
            }
            else
            {
                chars.Append(c);
            }
        }

        return chars.ToString();
    }
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

    /// <inheritdoc />
    protected override bool HasSortExpression => SortBy is not null;

    internal override IQueryable<TItem> ApplySort(IQueryable<TItem> source, bool descending) =>
        SortBy is null ? source : descending ? source.OrderByDescending(SortBy) : source.OrderBy(SortBy);

    internal override RenderFragment Cell(TItem item) => ChildContent?.Invoke(item) ?? (_ => { });
}

/// <summary>What a <see cref="Table{TItem}"/> asks its <c>ItemsProvider</c> for.</summary>
/// <param name="SortKey">The sorted column's key, or null.</param>
/// <param name="Descending">Whether the sort is descending.</param>
/// <param name="Page">The page, from 1.</param>
/// <param name="PageSize">Items per page, or null when the table doesn't page.</param>
public sealed record TableRequest<TItem>(string? SortKey, bool Descending, int Page, int? PageSize)
{
    internal ColumnBase<TItem>? Column { get; init; }

    /// <summary>Items to skip for this page.</summary>
    public int Skip => PageSize is { } size ? (Page - 1) * size : 0;

    /// <summary>Applies the requested sort to a query, using the column's own expression (EF Core translates it).</summary>
    public IQueryable<TItem> ApplySorting(IQueryable<TItem> source) => Column is null ? source : Column.ApplySort(source, Descending);

    /// <summary>Applies the requested sort and page to a query.</summary>
    public IQueryable<TItem> Apply(IQueryable<TItem> source)
    {
        var sorted = ApplySorting(source);
        return PageSize is { } size ? sorted.Skip(Skip).Take(size) : sorted;
    }
}

/// <summary>What an <c>ItemsProvider</c> returns: one page of items and the total count.</summary>
public sealed record TableResult<TItem>(IReadOnlyList<TItem> Items, int TotalItems);

/// <summary>Tells a table its columns have registered. Rendered after the columns, so its
/// initialization runs once they have; static rendering waits for the returned task.</summary>
internal sealed class TableColumnsReady : ComponentBase
{
    [Parameter] public Func<Task>? Ready { get; set; }

    protected override Task OnInitializedAsync() => Ready?.Invoke() ?? Task.CompletedTask;

    protected override bool ShouldRender() => false;
}
