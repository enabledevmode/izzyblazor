using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
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
    /// Adds a filter button to this column's header. In a Table: a text box for text (matching
    /// anywhere, ignoring case), Min and Max boxes for numbers (keeping the values between them;
    /// either may be empty), a date input for dates (matching the day), a list of checkboxes for enums
    /// and booleans. In a DataGrid, text, numbers and dates choose an operator above the value. A
    /// <see cref="TemplateColumn{TItem}"/> filters by its <c>FilterBy</c> text.
    /// </summary>
    [Parameter] public bool Filterable { get; set; }

    /// <summary>
    /// The operators this column's filter offers, the first being the default, such as
    /// <c>[FilterOperator.GreaterThanOrEqual, FilterOperator.Between]</c>; a condition with any other
    /// matches nothing, whoever writes it. With more than one, a Table's column chooses in a Select
    /// as a DataGrid's does. For text, numbers and dates.
    /// </summary>
    [Parameter] public IReadOnlyList<FilterOperator>? FilterOperators { get; set; }

    /// <summary>
    /// Builds this column's filter condition yourself, for a computed value or one stored
    /// differently from how it's shown. It gets the condition (operator and values parsed for the
    /// column's type) and returns the filter on the item, or null for the usual one. See
    /// <see cref="DataField{TItem}.Filter"/>.
    /// </summary>
    [Parameter] public Func<FieldFilter, Expression<Func<TItem, bool>>?>? Filter { get; set; }

    /// <summary>
    /// Makes this column's filter a list of checkboxes with a search, its values found here when the
    /// panel opens: a page at a time, narrowed by the other filters, and loading more on scroll. Use
    /// <see cref="FilterValuesRequest.ToResultAsync"/> to read them from a query. On a static page the
    /// script asks the page's own address for them, so a search renders the page again. With no script,
    /// the column shows its usual filter. Not with <see cref="FilterOperators"/>.
    /// </summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.Partial, NoScriptNotes = "With JavaScript off, the column shows its usual filter in place of the list.")]
    public FilterValuesLoader? FilterValues { get; set; }

    /// <summary>
    /// As <see cref="FilterValues"/>, reading a text column's values from the table's <c>Items</c>.
    /// With an <c>ItemsProvider</c> there's nothing to read, so use <see cref="FilterValues"/>.
    /// </summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.Partial, NoScriptNotes = "With JavaScript off, the column shows its usual filter in place of the list.")]
    public bool FilterList { get; set; }

    /// <summary>
    /// This column's own controls, in its filter panel above Clear and Apply. Spread the context's
    /// <see cref="FilterTemplateContext.Attributes"/> on a control and its value filters by the
    /// column's first operator, or several values by <see cref="FilterOperator.In"/>. With
    /// <see cref="Filter"/>, a value can mean anything. Works with no script. In a filter row, the cell
    /// opens the panel. Not with <see cref="FilterValues"/> or <see cref="FilterList"/>.
    /// </summary>
    [Parameter] public RenderFragment<FilterTemplateContext>? FilterTemplate { get; set; }

    /// <summary>
    /// What a <see cref="FilterTemplate"/>'s filter says in its row and tag, given its values, for
    /// values that aren't what people read. Defaults to the values themselves.
    /// </summary>
    [Parameter] public Func<IReadOnlyList<string?>, string>? FilterText { get; set; }

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

    /// <summary>
    /// In a grid with <c>SmallScreen="DataGridSmallScreen.HideColumns"</c>, which columns stay longest
    /// when it's narrow: higher stays longer. Defaults to 0; among equals, the last hides first. The
    /// first column and frozen ones never hide. Not for a Table.
    /// </summary>
    [Parameter] public int Priority { get; set; }

    /// <summary>In a grid with <c>ResizableColumns</c>, how wide people can make this column. No limit by default.</summary>
    [Parameter] public Length? MaxWidth { get; set; }

    /// <summary>In a grid with <c>ResizableColumns</c>, whether people can resize this column. Defaults to true.</summary>
    [Parameter] public bool Resizable { get; set; } = true;

    /// <summary>In a grid with <c>ReorderableColumns</c>, whether people can move this column. Defaults to true.</summary>
    [Parameter] public bool Reorderable { get; set; } = true;

    /// <summary>In a grid with a <c>ColumnPicker</c>, whether people can hide this column. Defaults to true.</summary>
    [Parameter] public bool Pickable { get; set; } = true;

    /// <summary>
    /// Whether the column shows at first in a <c>DataGrid</c>. Hidden, it's still in the column picker,
    /// where people can show it, unless <see cref="Pickable"/> is false, and can still be filtered by and
    /// edited. This isn't access control: its values are sent once it's shown, so leave out a column
    /// someone mustn't see.
    /// </summary>
    [Parameter] public bool Visible { get; set; } = true;

    /// <summary>
    /// Whether a row's editor has a field for this column, in a <c>DataGrid</c> with an <c>EditMode</c>.
    /// Only editable columns take a value from a save, so a forged one changes nothing. By default a
    /// column is editable if it can be, shown or not: a settable property of the row, of a type with a
    /// field, without <c>[Key]</c>, <c>[Editable(false)]</c> or <c>[ReadOnly(true)]</c>. Set it to false
    /// on what people mustn't change, such as the row's key.
    /// </summary>
    [Parameter] public bool? Editable { get; set; }

    /// <summary>
    /// Your own field for the column, in place of the one for its type. Bind it to <c>context.Item</c>,
    /// and pass <c>context.Attributes</c>: <c>&lt;Select @bind-Value="context.Item.OwnerId" Items="owners" @attributes="context.Attributes" /&gt;</c>.
    /// </summary>
    [Parameter] public RenderFragment<DataGridEditField<TItem>>? EditTemplate { get; set; }

    /// <summary>A class for this column's cells and header.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Text for this column in the table's footer row, such as a total.</summary>
    [Parameter] public string? FooterText { get; set; }

    /// <summary>Content for this column in the table's footer row, in place of <see cref="FooterText"/>.</summary>
    [Parameter] public RenderFragment? Footer { get; set; }

    /// <summary>
    /// What this column shows in the grid's footer, worked out over every row the filters match, not
    /// just the page. Grouping isn't needed. Not for a Table.
    /// </summary>
    [Parameter] public DataAggregate? Aggregate { get; set; }

    /// <summary>What this column shows on each group's row, over the whole group, open or not.</summary>
    [Parameter] public DataAggregate? GroupAggregate { get; set; }

    /// <summary>What this column shows on a row under an open group's rows, over the whole group.</summary>
    [Parameter] public DataAggregate? GroupFooterAggregate { get; set; }

    /// <summary>
    /// Your own aggregate, as an expression over the rows, so it runs in the database:
    /// <c>g =&gt; g.Sum(o =&gt; o.Profit) / g.Sum(o =&gt; o.Total)</c>. Wherever the column's aggregate
    /// is <see cref="DataAggregate.Custom"/>; set alone, it's the footer's. Not for a Table.
    /// </summary>
    [Parameter] public Expression<Func<IEnumerable<TItem>, object?>>? AggregateBy { get; set; }

    /// <summary>A .NET format string for the aggregate, where every place shows the same one. Defaults to the column's own, except for a count or your own aggregate.</summary>
    [Parameter] public string? AggregateFormat { get; set; }

    /// <summary>The word before the aggregate, such as "Total", where every place shows the same one; empty for none. Defaults to the aggregate's name, and to none for your own.</summary>
    [Parameter] public string? AggregateLabel { get; set; }

    /// <summary>
    /// In a grid that groups, whether this column can be grouped by. Defaults to true, for a column
    /// with one value to compare.
    /// </summary>
    [Parameter] public bool Groupable { get; set; } = true;

    /// <summary>
    /// What to group by, when it isn't the column's value: for a <see cref="TemplateColumn{TItem}"/>, or a
    /// coarser key such as <c>o =&gt; o.Placed.Year</c>. It runs in the database, so keep it to what yours can
    /// translate. "Show only this group" isn't offered, as no filter on the column can say it.
    /// </summary>
    [Parameter] public Expression<Func<TItem, object?>>? GroupBy { get; set; }

    /// <summary>A .NET format string for a group's value. Defaults to the column's own when it has no <see cref="GroupBy"/>.</summary>
    [Parameter] public string? GroupFormat { get; set; }

    /// <summary>A group's label, in place of the column's title and the value. The count stays beside it.</summary>
    [Parameter] public RenderFragment<DataGridGroup>? GroupTemplate { get; set; }

    /// <summary>An aggregate's cell, in place of its word and value, wherever it shows. In the footer, the group is all the rows.</summary>
    [Parameter] public RenderFragment<DataGridAggregate>? AggregateTemplate { get; set; }

    internal bool CanGroup => Groupable && GroupType is not null;

    /// <summary>Its name in the grouping: a template column needn't have a key.</summary>
    internal string GroupToken => AggregateKey;

    /// <summary>The field a group request names.</summary>
    internal string GroupField => GroupBy is null ? GroupToken : $"{GroupToken}.group";

    // Without the box a lambda to object puts on: a group compares the value itself.
    private LambdaExpression? GroupByValue => GroupBy is null ? null
        : Expression.Lambda(GroupBy.Body is UnaryExpression { NodeType: ExpressionType.Convert } box && box.Type == typeof(object) ? box.Operand : GroupBy.Body, GroupBy.Parameters);

    /// <summary>The type of the value it groups by, or null when it can't.</summary>
    internal Type? GroupType => GroupBy is null ? OwnGroupType : FilterConvert.KindOf(GroupByValue!.ReturnType) == FieldKind.None ? null : GroupByValue.ReturnType;

    /// <summary>The type of the column's own value, if it can group by it.</summary>
    private protected virtual Type? OwnGroupType => null;

    /// <summary>A field for <see cref="GroupBy"/>, which isn't the column's own.</summary>
    internal DataField<TItem>? GroupByField => GroupByValue is not { } value || GroupType is null ? null : new()
    {
        Key = GroupField,
        Value = value,
        Caption = HeaderText,
        Sortable = true,
        Filterable = true,
        Groupable = true,
    };

    /// <summary>A group's value as the column's type: it may have come over the wire.</summary>
    internal object? GroupValue(object? value)
    {
        // Text stays as it is, spaces and all, as the database grouped it.
        var plain = FilterConvert.Unwrap(value);
        return plain is not null and not string || GroupType != typeof(string)
            ? plain is not null && GroupType is { } type && FilterConvert.TryConvert(type, plain, out var typed) ? typed : plain
            : plain;
    }

    /// <summary>A group's value for people, in the column's format.</summary>
    internal string? GroupText(object? value) =>
        value is null ? null : (GroupFormat ?? (GroupBy is null ? ValueFormat : null)) is { } format && value is IFormattable formattable ? formattable.ToString(format, CultureInfo.CurrentCulture) : value.ToString();

    /// <summary>The operators for a request: an open group finds its rows by equality, whatever people can choose.</summary>
    private protected IReadOnlyList<FilterOperator>? RequestOperators(IReadOnlyList<FilterOperator>? operators) =>
        operators is null || GroupBy is not null || Table?.GroupsBy(this) != true || operators.Contains(FilterOperator.Equals) ? operators : [.. operators, FilterOperator.Equals];

    /// <summary>What it shows in a place, if anything. AggregateBy alone is the footer's.</summary>
    internal DataAggregate? AggregateIn(AggregatePlace place) => place switch
    {
        AggregatePlace.Footer => Aggregate ?? (AggregateBy is not null && GroupAggregate is null && GroupFooterAggregate is null ? DataAggregate.Custom : null),
        AggregatePlace.GroupRow => GroupAggregate,
        _ => GroupFooterAggregate,
    };

    internal IEnumerable<DataAggregate> Aggregates => new[] { AggregatePlace.Footer, AggregatePlace.GroupRow, AggregatePlace.GroupFooter }
        .Select(AggregateIn).OfType<DataAggregate>().Distinct();

    internal bool HasAggregate => Aggregates.Any();

    /// <summary>The aggregate's name in a request. A template column needn't have a key.</summary>
    internal string AggregateKey => Key ?? HeaderText;

    internal bool HasFooter => Footer is not null || FooterText is not null || AggregateIn(AggregatePlace.Footer) is not null;

    internal RenderFragment? WrittenFooter => Footer ?? (FooterText is null ? null : b => b.AddContent(0, FooterText));

    /// <summary>The column's own format, which its aggregate takes.</summary>
    internal virtual string? ValueFormat => null;

    /// <summary>Why an aggregate can't work on this column, or null when it can.</summary>
    private protected virtual string? AggregateProblem(DataAggregate type) => null;

    /// <summary>An aggregate of a group, as the column's type where it can be; whether the group has it.</summary>
    internal bool TryAggregate(DataGroup? group, DataAggregate type, out object? value)
    {
        value = null;
        // By the name asked for, however a server's answer wrote it.
        var name = new DataAggregateField(AggregateKey, type).ToString();
        var found = group?.Values.FirstOrDefault(v => string.Equals(v.Key, name, StringComparison.OrdinalIgnoreCase)) ?? default;
        if (found.Key is null)
        {
            return false;
        }

        value = FilterConvert.Unwrap(found.Value);
        if (type is DataAggregate.Min or DataAggregate.Max && ValueType is { } valueType && FilterConvert.TryConvert(valueType, value, out var typed))
        {
            value = typed;
        }

        return true;
    }

    /// <summary>An aggregate as text, in its format.</summary>
    internal string? AggregateText(DataAggregate type, object? value)
    {
        var format = AggregateFormat ?? (type is DataAggregate.Count or DataAggregate.Custom ? null : ValueFormat) ?? (type == DataAggregate.Avg ? "0.##" : null);
        return format is not null && value is IFormattable formattable ? formattable.ToString(format, CultureInfo.CurrentCulture) : value?.ToString();
    }

    /// <summary>The word before an aggregate.</summary>
    internal string? AggregateWord(IzzyText text, DataAggregate type) => AggregateLabel ?? type switch
    {
        DataAggregate.Sum => text.AggregateSum,
        DataAggregate.Avg => text.AggregateAvg,
        DataAggregate.Min => text.AggregateMin,
        DataAggregate.Max => text.AggregateMax,
        DataAggregate.Count => text.AggregateCount,
        _ => null,
    };

    [CascadingParameter] internal TableViewBase<TItem>? Table { get; set; }

    internal string HeaderText => Title ?? DefaultTitle ?? "";

    internal string? Key => SortKey ?? DefaultSortKey;

    internal bool CanSort => Sortable && Key is not null && HasSortExpression;

    internal bool CanFilter => Filterable && Key is not null && FilterKind != ColumnFilterKind.None;

    /// <summary>Whether the column's filter lists values that are looked up, as an enum's aren't.</summary>
    internal bool HasValueList => FilterValues is not null || FilterList;

    /// <summary>What kind of filter the column offers.</summary>
    internal virtual ColumnFilterKind FilterKind => ColumnFilterKind.None;

    /// <summary>The rows of a list filter: value and text.</summary>
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

    internal bool IsEditable => Editable ?? EditableByDefault;

    /// <summary>Whether it's editable when <see cref="Editable"/> isn't set.</summary>
    internal bool EditableByDefault =>
        EditProblem is null
        && EditProperty is { } property
        && property.GetCustomAttribute<KeyAttribute>() is null
        && property.GetCustomAttribute<EditableAttribute>() is not { AllowEdit: false }
        && property.GetCustomAttribute<System.ComponentModel.ReadOnlyAttribute>() is not { IsReadOnly: true };

    /// <summary>Its name in an <c>EditField</c>.</summary>
    internal string? EditKey => Key ?? EditProperty?.Name.ToLowerInvariant();

    /// <summary>The property a save writes.</summary>
    internal virtual PropertyInfo? EditProperty => null;

    /// <summary>Why <see cref="Editable"/> can't work on this column, or null when it can.</summary>
    protected virtual string? EditProblem => $"The \"{HeaderText}\" column can't be edited: only a PropertyColumn can, for now.";

    /// <summary>
    /// The field that edits this column on <paramref name="model"/>, posting under <paramref name="name"/>.
    /// With <paramref name="rowForm"/> it's in a row, and joins that form from there.
    /// </summary>
    internal virtual RenderFragment Editor(TItem model, string name, object receiver, Func<bool> autofocus, Func<EditField?> placed, string? rowForm) => builder =>
    {
        var field = placed();
        var attributes = new Dictionary<string, object> { ["name"] = name, ["Label"] = field?.Label ?? HeaderText };
        if (rowForm is not null)
        {
            attributes["form"] = rowForm;
            attributes["HideLabel"] = true;
            attributes["Size"] = ControlSize.Sm;
        }

        if (field?.Hint is { } hint)
        {
            attributes["Hint"] = hint;
        }

        if (field is { ColSpan.IsEmpty: false })
        {
            attributes["ColSpan"] = field.ColSpan;
        }

        if (autofocus())
        {
            attributes["autofocus"] = true;
        }

        builder.AddContent(0, EditTemplate, new DataGridEditField<TItem>(model, name, attributes));
    };

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

        if (Priority != 0 && Table is { GridColumns: false })
        {
            throw new InvalidOperationException($"The \"{HeaderText}\" column has a {nameof(Priority)}, which only a DataGrid's columns take.");
        }

        if (Priority != 0 && Table is DataGrid<TItem> { SmallScreen: not DataGridSmallScreen.HideColumns })
        {
            throw new InvalidOperationException($"The \"{HeaderText}\" column has a {nameof(Priority)}, which only a DataGrid with SmallScreen=\"HideColumns\" uses.");
        }

        if (Priority != 0 && Frozen != ColumnFrozen.None)
        {
            throw new InvalidOperationException($"The \"{HeaderText}\" column has a {nameof(Priority)}, but it's {nameof(Frozen)}, and frozen columns never hide.");
        }

        if (!Visible && Table is { GridColumns: false })
        {
            throw new InvalidOperationException($"The \"{HeaderText}\" column sets {nameof(Visible)}, which only a DataGrid's columns take.");
        }

        if (HasAggregate)
        {
            if (Table is { GridColumns: false })
            {
                throw new InvalidOperationException($"The \"{HeaderText}\" column has an aggregate, which only a DataGrid's columns take.");
            }

            foreach (var type in Aggregates)
            {
                if (type == DataAggregate.Custom && AggregateBy is null)
                {
                    throw new InvalidOperationException($"The \"{HeaderText}\" column has a Custom aggregate, so it needs an {nameof(AggregateBy)}.");
                }

                if (type != DataAggregate.Custom && AggregateProblem(type) is { } aggregate)
                {
                    throw new InvalidOperationException(aggregate);
                }
            }

            if (AggregateBy is not null && !Aggregates.Contains(DataAggregate.Custom))
            {
                throw new InvalidOperationException($"The \"{HeaderText}\" column has an {nameof(AggregateBy)} that no place shows: set Custom where it goes.");
            }

            if ((AggregateLabel is not null || AggregateFormat is not null) && Aggregates.Skip(1).Any())
            {
                throw new InvalidOperationException($"The \"{HeaderText}\" column shows different aggregates in different places, so one {nameof(AggregateLabel)} or {nameof(AggregateFormat)} can't fit them all.");
            }
        }

        if (Editable == true)
        {
            if (Table is { GridColumns: false })
            {
                throw new InvalidOperationException($"The \"{HeaderText}\" column is {nameof(Editable)}, which only a DataGrid's columns can be.");
            }

            if (EditProblem is { } editProblem)
            {
                throw new InvalidOperationException(editProblem);
            }
        }

        if (Table is { GridColumns: true })
        {
            if (Frozen != ColumnFrozen.None && Width is null)
            {
                throw new InvalidOperationException($"The \"{HeaderText}\" column is {nameof(Frozen)}, so it needs a {nameof(Width)}: the columns after it are placed by adding the widths up.");
            }

            // The grid adds its widths up, and a percent of the table or a keyword can't be added.
            foreach (var (name, length) in new[] { (nameof(Width), Width), (nameof(MinWidth), MinWidth), (nameof(MaxWidth), MaxWidth) })
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

        if (ColumnFilter.OperatorsProblem(this) is { } operators)
        {
            throw new InvalidOperationException(operators);
        }

        if (ColumnFilter.ValueListProblem(this) is { } list)
        {
            throw new InvalidOperationException(list);
        }
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => false;

    private object? _shown;

    /// <summary>What the table shows for this column, apart from cell templates: a change re-renders the table. Equal when nothing changed, as a list written in place is a new one on each render.</summary>
    protected virtual object Shown => (Title, Sortable, SortKey, Align, Width, MinWidth, MaxWidth, Resizable, Reorderable, Pickable, Frozen, Class, FooterText, Footer is not null, Filterable, FilterOperators is null ? null : string.Join(',', FilterOperators), HasValueList, FilterTemplate is not null, Editable, Visible, (Aggregate, GroupAggregate, GroupFooterAggregate, AggregateBy is not null, AggregateFormat, AggregateLabel, Groupable, GroupFormat, GroupBy is not null));

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

    internal static bool IsNumeric(Type type) => FilterConvert.IsNumeric(type);
}

/// <summary>Where a column's aggregate shows.</summary>
internal enum AggregatePlace
{
    Footer,
    GroupRow,
    GroupFooter,
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
public class PropertyColumn<TItem, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue> : ColumnBase<TItem>
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
    internal override string? ValueFormat => Format;

    /// <inheritdoc />
    private protected override Type? OwnGroupType => FilterConvert.KindOf(typeof(TValue)) == FieldKind.None ? null : typeof(TValue);

    /// <inheritdoc />
    private protected override string? AggregateProblem(DataAggregate type) => type switch
    {
        DataAggregate.Sum or DataAggregate.Avg when FilterConvert.KindOf(typeof(TValue)) != FieldKind.Number =>
            $"The \"{HeaderText}\" column has a {type}, which only numbers take.",
        DataAggregate.Min or DataAggregate.Max when FilterConvert.KindOf(typeof(TValue)) is not (FieldKind.Number or FieldKind.Date or FieldKind.Time or FieldKind.Text) =>
            $"The \"{HeaderText}\" column has a {type}, which {typeof(TValue).Name} values don't take.",
        _ => null,
    };

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
        Filterable = CanFilter || (GroupBy is null && Table?.GroupsBy(this) == true),
        Searchable = FilterKind == ColumnFilterKind.Text,
        Groupable = GroupBy is null && CanGroup,
        Aggregatable = HasAggregate,
        AggregateBy = AggregateBy,
        Operators = RequestOperators(ColumnFilter.FieldOperators(this)),
        Case = Table?.FilterCase ?? FilterCase.IgnoreCase,
        Choices = FilterKind == ColumnFilterKind.Choice ? FilterChoices(text) : null,
        Filter = Filter,
        FilterValues = FilterValues,
    };

    internal override RenderFragment Cell(TItem item) => builder =>
    {
        var value = _compiled!(item);
        builder.AddContent(0, Format is not null && value is IFormattable formattable
            ? formattable.ToString(Format, CultureInfo.CurrentCulture)
            : value?.ToString());
    };

    private static readonly Type Underlying = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);

    // A row's own property: one further along would be shared with the row, as its copy is shallow.
    internal override PropertyInfo? EditProperty =>
        Property?.Body is MemberExpression { Expression: ParameterExpression, Member: PropertyInfo property } ? property : null;

    /// <inheritdoc />
    protected override string? EditProblem
    {
        get
        {
            if (EditProperty is not { CanWrite: true })
            {
                return $"The \"{HeaderText}\" column can't be edited: only a settable property of the row itself can, not a nested one or a computed one.";
            }

            return EditTemplate is not null || Underlying == typeof(string) || Underlying.IsEnum || typeof(TValue) == typeof(bool) || IsNumeric(Underlying)
                || Underlying == typeof(DateOnly) || Underlying == typeof(DateTime) || Underlying == typeof(DateTimeOffset)
                || Underlying == typeof(TimeOnly) || Underlying == typeof(TimeSpan)
                ? null
                : $"The \"{HeaderText}\" column can't be edited: there's no field for {typeof(TValue).Name} values.";
        }
    }

    /// <inheritdoc />
    internal override RenderFragment Editor(TItem model, string name, object receiver, Func<bool> autofocus, Func<EditField?> placed, string? rowForm)
    {
        if (EditTemplate is not null)
        {
            return base.Editor(model, name, receiver, autofocus, placed, rowForm);
        }

        var property = EditProperty!;
        var value = Expression.Lambda<Func<TValue>>(Expression.Property(Expression.Constant(model, typeof(TItem)), property));
        var changed = EventCallback.Factory.Create<TValue>(receiver, v => property.SetValue(model, v));
        return builder =>
        {
            var field = placed();
            OpenField(builder);
            builder.AddComponentParameter(1, "Label", field?.Label ?? HeaderText);
            builder.AddComponentParameter(2, "Value", (TValue)property.GetValue(model)!);
            builder.AddComponentParameter(3, "ValueChanged", changed);
            builder.AddComponentParameter(4, "ValueExpression", value);
            builder.AddComponentParameter(5, "name", name);
            if (autofocus())
            {
                builder.AddComponentParameter(6, "autofocus", true);
            }

            if (Underlying.IsEnum)
            {
                var choices = DataFields.EnumChoices(Underlying)!;
                builder.AddComponentParameter(7, "Items", choices.Select(c => (TValue)Enum.Parse(Underlying, c.Value)).ToList());
                builder.AddComponentParameter(8, "ItemText", (Func<TValue, string>)(v => choices.FirstOrDefault(c => c.Value == v?.ToString()).Text ?? ""));
            }

            if (field?.Hint is { } hint)
            {
                builder.AddComponentParameter(9, "Hint", hint);
            }

            if (field is { ColSpan.IsEmpty: false })
            {
                builder.AddComponentParameter(10, "ColSpan", field.ColSpan);
            }

            if (rowForm is not null)
            {
                builder.AddComponentParameter(11, "form", rowForm);
                builder.AddComponentParameter(12, "Size", ControlSize.Sm);
                if (typeof(TValue) != typeof(bool))
                {
                    builder.AddComponentParameter(13, "HideLabel", true);
                }
            }

            builder.CloseComponent();
        };
    }

    // Each named outright, so trimming keeps what the column's type can use.
    private void OpenField(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        if (Underlying == typeof(string))
        {
            if (EditProperty?.GetCustomAttribute<DataTypeAttribute>()?.DataType == DataType.MultilineText)
            {
                builder.OpenComponent<TextArea>(0);
            }
            else
            {
                builder.OpenComponent<TextField>(0);
            }
        }
        else if (typeof(TValue) == typeof(bool))
        {
            builder.OpenComponent<Checkbox>(0);
        }
        else if (Underlying.IsEnum)
        {
            builder.OpenComponent<Select<TValue>>(0);
        }
        else if (Underlying == typeof(TimeOnly))
        {
            builder.OpenComponent<TimeField<TValue>>(0);
        }
        else if (Underlying == typeof(TimeSpan))
        {
            builder.OpenComponent<DurationField<TValue>>(0);
        }
        else if (Underlying == typeof(DateOnly) || Underlying == typeof(DateTime) || Underlying == typeof(DateTimeOffset))
        {
            builder.OpenComponent<DateField<TValue>>(0);
        }
        else
        {
            builder.OpenComponent<NumberField<TValue>>(0);
        }
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

    /// <summary>
    /// The text to filter by when <see cref="ColumnBase{TItem}.Filterable"/>, such as
    /// <c>o =&gt; o.Customer.Name</c>. It matches anywhere, ignoring case. Needs a
    /// <see cref="ColumnBase{TItem}.SortKey"/>.
    /// </summary>
    [Parameter] public Expression<Func<TItem, string?>>? FilterBy { get; set; }

    /// <summary>
    /// The property of the row this column edits, with an <see cref="ColumnBase{TItem}.EditTemplate"/>:
    /// <c>o =&gt; o.OwnerId</c>. A template column isn't editable without both.
    /// </summary>
    [Parameter] public Expression<Func<TItem, object?>>? EditBy { get; set; }

    /// <inheritdoc />
    internal override PropertyInfo? EditProperty =>
        (EditBy?.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : EditBy?.Body)
            is MemberExpression { Expression: ParameterExpression, Member: PropertyInfo property } ? property : null;

    /// <inheritdoc />
    protected override string? EditProblem =>
        EditBy is null || EditTemplate is null ? $"The \"{HeaderText}\" column needs {nameof(EditBy)} and an {nameof(EditTemplate)} to be edited."
        : EditProperty is not { CanWrite: true } ? $"The \"{HeaderText}\" column's {nameof(EditBy)} must be a settable property of the row itself."
        : null;

    /// <inheritdoc />
    private protected override string? AggregateProblem(DataAggregate type) => type == DataAggregate.Count ? null : $"The \"{HeaderText}\" column has no one value to work out a {type} on: use {nameof(AggregateBy)}.";

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
    internal override DataField<TItem>? ToField(IzzyText text) => Key is not { } key || (FilterBy ?? (LambdaExpression?)SortBy) is not { } value
        ? !HasAggregate ? null : new()
        {
            // Nothing to read but the aggregate.
            Key = AggregateKey,
            Value = (Expression<Func<TItem, int>>)(_ => 0),
            Caption = HeaderText,
            Aggregatable = true,
            AggregateBy = AggregateBy,
        }
        : new()
    {
        Key = key,
        Value = value,
        Aggregatable = HasAggregate,
        AggregateBy = AggregateBy,
        SortValue = FilterBy is null ? null : SortBy,
        Caption = HeaderText,
        Sortable = CanSort,
        Filterable = CanFilter,
        Searchable = FilterBy is not null,
        Operators = ColumnFilter.FieldOperators(this),
        Case = Table?.FilterCase ?? FilterCase.IgnoreCase,
        Filter = Filter,
        FilterValues = FilterValues,
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
