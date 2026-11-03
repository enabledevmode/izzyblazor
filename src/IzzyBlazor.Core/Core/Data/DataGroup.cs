using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace IzzyBlazor;

/// <summary>What a <c>DataGrid</c> column works out over a group of rows, or over all of them.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DataAggregate>))]
public enum DataAggregate
{
    /// <summary>The values added up. For numbers.</summary>
    Sum,

    /// <summary>The mean of the values. For numbers.</summary>
    Avg,

    /// <summary>The lowest value. For numbers, dates, times and text.</summary>
    Min,

    /// <summary>The highest value. For numbers, dates, times and text.</summary>
    Max,

    /// <summary>How many rows.</summary>
    Count,

    /// <summary>The field's own <see cref="DataField{TItem}.AggregateBy"/>.</summary>
    Custom,
}

/// <summary>One aggregate a <see cref="DataGroupRequest{TItem}"/> asks for.</summary>
/// <param name="Key">The field's key, such as <c>"total"</c>.</param>
/// <param name="Type">What to work out.</param>
public readonly record struct DataAggregateField(string Key, DataAggregate Type)
{
    /// <summary>The aggregate as the address writes it: <c>"total:sum"</c>.</summary>
    public override string ToString() => $"{Key}:{Type.ToString().ToLowerInvariant()}";

    /// <summary>Aggregates from the address's form: <c>"total:sum,qty:avg"</c>. What doesn't read as one is left out.</summary>
    public static IReadOnlyList<DataAggregateField> ParseList(string? text)
    {
        var list = new List<DataAggregateField>();
        foreach (var part in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = part.LastIndexOf(':');
            if (colon > 0 && Enum.TryParse<DataAggregate>(part[(colon + 1)..], ignoreCase: true, out var type) && Enum.IsDefined(type))
            {
                list.Add(new DataAggregateField(part[..colon], type));
            }
        }

        return list;
    }
}

/// <summary>
/// What a <c>DataGrid</c> asks its <c>GroupsProvider</c> for: one level of groups with their counts
/// and aggregates, or, with no <see cref="Keys"/>, the one group of everything that matches, which
/// is the grand total. Rows aren't part of it: an open group's rows come from the <c>ItemsProvider</c>,
/// with the group's values in its filter. Answer it with <c>ToResultAsync</c>.
/// </summary>
/// <typeparam name="TItem">The type of the items.</typeparam>
public sealed record DataGroupRequest<TItem>
{
    /// <summary>The most levels a request can group by; any more are left out.</summary>
    public const int MaxLevels = 4;

    /// <summary>The fields to group by, outermost first, and the order of each; empty for the grand total.</summary>
    public IReadOnlyList<SortField> Keys { get; init; } = [];

    /// <summary>What to work out for each group, besides its count.</summary>
    public IReadOnlyList<DataAggregateField> Aggregates { get; init; } = [];

    /// <summary>The filter, or null for none.</summary>
    public Filter? Filter { get; init; }

    /// <summary>Free text to look for, in the searchable fields; null for none.</summary>
    public string? Search { get; init; }

    /// <summary>Groups to skip.</summary>
    public int Skip { get; init; }

    /// <summary>Groups to take; null for every group.</summary>
    public int? Take { get; init; }

    /// <summary>Whether the component needs how many groups there are in all.</summary>
    public bool NeedsTotal { get; init; }

    /// <summary>Canceled when the component no longer needs this answer.</summary>
    [JsonIgnore]
    public CancellationToken CancellationToken { get; init; }

    /// <summary>The fields the component attached (its columns).</summary>
    internal IReadOnlyList<DataField<TItem>>? Fields { get; init; }

    private IReadOnlyList<DataField<TItem>> AttachedFields => Fields ?? throw new InvalidOperationException(
        $"This request came without its component's fields (over the wire, or made in code): pass them, such as ToResult(query, DataFields.Of<{typeof(TItem).Name}>()).");

    /// <summary>The groups, from an in-memory list or a synchronous query, with the component's fields.</summary>
    public DataGroupResult ToResult(IQueryable<TItem> source) => ToResult(source, AttachedFields);

    /// <summary>
    /// The groups, through these fields only, for a request that came over the wire: a key that
    /// isn't <see cref="DataField{TItem}.Groupable"/> or an aggregate that isn't
    /// <see cref="DataField{TItem}.Aggregatable"/> is left out, so a request can't reach other properties.
    /// </summary>
    public DataGroupResult ToResult(IQueryable<TItem> source, IReadOnlyList<DataField<TItem>> fields)
    {
        var query = Plan(fields);
        var filtered = Filtered(source, fields);
        return query.Read(query.Rows(filtered).ToList(), NeedsTotal && query.Levels > 0 ? query.Count(filtered).Count() : null);
    }

    /// <summary>
    /// The groups through your async methods, so Core needs no EF Core:
    /// <c>count: (q, ct) =&gt; q.CountAsync(ct), list: (q, ct) =&gt; q.ToListAsync(ct)</c>.
    /// </summary>
    public Task<DataGroupResult> ToResultAsync(IQueryable<TItem> source,
        Func<IQueryable<object?[]>, CancellationToken, Task<int>> count,
        Func<IQueryable<object?[]>, CancellationToken, Task<List<object?[]>>> list) =>
        ToResultAsync(source, AttachedFields, count, list);

    /// <summary>The groups through your async methods, through these fields only.</summary>
    public async Task<DataGroupResult> ToResultAsync(IQueryable<TItem> source, IReadOnlyList<DataField<TItem>> fields,
        Func<IQueryable<object?[]>, CancellationToken, Task<int>> count,
        Func<IQueryable<object?[]>, CancellationToken, Task<List<object?[]>>> list)
    {
        var query = Plan(fields);
        var filtered = Filtered(source, fields);
        int? total = NeedsTotal && query.Levels > 0 ? await count(query.Count(filtered), CancellationToken) : null;
        return query.Read(await list(query.Rows(filtered), CancellationToken), total);
    }

    /// <summary>
    /// The request as a query string, for a GET API: <c>group=status,-customer&amp;aggregate=total:sum&amp;skip=20&amp;take=10&amp;filter=...&amp;search=...</c>,
    /// with <c>total=true</c> when it needs the number of groups. Read it back with <see cref="Parse"/>.
    /// </summary>
    public string ToQueryString()
    {
        var parts = new List<string>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                parts.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }

        Add("group", Keys.Count == 0 ? null : string.Join(",", Keys));
        Add("aggregate", Aggregates.Count == 0 ? null : string.Join(",", Aggregates));
        Add("skip", Skip > 0 ? Skip.ToString(CultureInfo.InvariantCulture) : null);
        Add("take", Take?.ToString(CultureInfo.InvariantCulture));
        Add("filter", Filter?.ToString());
        Add("search", Search);
        Add("total", NeedsTotal ? "true" : null);
        return string.Join("&", parts);
    }

    /// <summary>Reads a request from a query string (<see cref="ToQueryString"/>), or a URL with one. A filter that doesn't parse matches nothing.</summary>
    public static DataGroupRequest<TItem> Parse(string query)
    {
        var at = query.IndexOf('?', StringComparison.Ordinal);
        var values = QueryString.Parse(at >= 0 ? query[(at + 1)..] : query);
        string? Get(string key) => values.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v.ToString()) ? v.ToString() : null;
        int? Number(string key) => int.TryParse(Get(key), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;

        var filterText = Get("filter");
        return new DataGroupRequest<TItem>
        {
            Keys = SortField.ParseList(Get("group")),
            Aggregates = DataAggregateField.ParseList(Get("aggregate")),
            Skip = Number("skip") ?? 0,
            Take = Number("take"),
            Filter = filterText is null ? null : IzzyBlazor.Filter.TryParse(filterText, out var filter) ? filter : IzzyBlazor.Filter.Any(),
            Search = Get("search"),
            NeedsTotal = string.Equals(Get("total"), "true", StringComparison.OrdinalIgnoreCase),
        };
    }

    private IQueryable<TItem> Filtered(IQueryable<TItem> source, IReadOnlyList<DataField<TItem>> fields) =>
        new DataRequest<TItem> { Filter = Filter, Search = Search }.ApplyFiltering(source, fields);

    private GroupQuery<TItem> Plan(IReadOnlyList<DataField<TItem>> fields)
    {
        DataField<TItem>? Field(string key) => fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
        var keys = Keys
            .Select(k => (Sort: k, Field: Field(k.Key)))
            .Where(k => k.Field is { Groupable: true })
            .DistinctBy(k => k.Field!.Key, StringComparer.OrdinalIgnoreCase)
            .Take(MaxLevels)
            .Select(k => (k.Field!, k.Sort.Descending))
            .ToList();
        var aggregates = Aggregates
            .Select(a => (a.Type, Field: Field(a.Key)))
            .Where(a => a.Field is { Aggregatable: true } && GroupQuery<TItem>.Takes(a.Field, a.Type))
            .Select(a => (a.Field!, a.Type))
            .Distinct()
            .ToList();
        return new GroupQuery<TItem>(keys, aggregates, Skip, Take);
    }
}

/// <summary>What a <c>GroupsProvider</c> returns.</summary>
/// <param name="Groups">The groups, in order. For a grand total, one.</param>
/// <param name="Total">How many groups there are in all, when the request's <c>NeedsTotal</c> asks.</param>
public sealed record DataGroupResult(IReadOnlyList<DataGroup> Groups, int? Total = null);

/// <summary>One group: its values, how many rows are in it, and what was worked out over them.</summary>
/// <param name="Keys">Its value at each level the request grouped by, outermost first.</param>
/// <param name="Count">How many rows match, in the whole group.</param>
public sealed record DataGroup(IReadOnlyList<object?> Keys, int Count)
{
    /// <summary>The aggregates, by field key and type as <see cref="DataAggregateField"/> writes them: <c>"total:sum"</c>.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; init; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}

// A key of several values. A record, as a list in memory groups by Equals: a plain class would make
// a group of each row. EF Core reads the members, whatever their type.
internal sealed record GroupKey<T0>(T0 K0)
{
    public GroupKey() : this(default(T0)!) { }
}

internal sealed record GroupKey<T0, T1>(T0 K0, T1 K1)
{
    public GroupKey() : this(default!, default!) { }
}

internal sealed record GroupKey<T0, T1, T2>(T0 K0, T1 K1, T2 K2)
{
    public GroupKey() : this(default!, default!, default!) { }
}

internal sealed record GroupKey<T0, T1, T2, T3>(T0 K0, T1 K1, T2 K2, T3 K3)
{
    public GroupKey() : this(default!, default!, default!, default!) { }
}

/// <summary>
/// The group query, built from fields: <c>GROUP BY</c> the keys, in their order, with the count and
/// aggregates as one row of values. The same expression runs on a list and in a database.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The DynamicDependencies on Grouped keep the key types' members, and an expression reads only those and the fields' own.")]
[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "As above; the type arguments are the fields' own.")]
[UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "The DynamicDependencies on Grouped keep the Queryable and Enumerable methods.")]
[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "As above.")]
[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "As above.")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "WebAssembly AOT falls back to its interpreter for an instantiation it did not compile.")]
internal sealed class GroupQuery<TItem>(
    IReadOnlyList<(DataField<TItem> Field, bool Descending)> keys,
    IReadOnlyList<(DataField<TItem> Field, DataAggregate Type)> aggregates,
    int skip,
    int? take)
{
    public int Levels => keys.Count;

    /// <summary>Whether an aggregate can be worked out on a field of its type.</summary>
    public static bool Takes(DataField<TItem> field, DataAggregate type) => type switch
    {
        DataAggregate.Count => true,
        DataAggregate.Custom => field.AggregateBy is not null,
        DataAggregate.Sum or DataAggregate.Avg => FilterConvert.KindOf(field.Type) == FieldKind.Number,
        _ => FilterConvert.KindOf(field.Type) is FieldKind.Number or FieldKind.Date or FieldKind.Time or FieldKind.Text,
    };

    /// <summary>A row per group: its keys, its count, its aggregates, ordered and paged.</summary>
    public IQueryable<object?[]> Rows(IQueryable<TItem> source)
    {
        var (grouped, groupType) = Grouped(source);
        var ordered = grouped.Expression;
        for (var i = 0; i < keys.Count; i++)
        {
            var g = Expression.Parameter(groupType, "g");
            var key = Expression.Lambda(KeyMember(g, i), g);
            var method = (i == 0, keys[i].Descending) switch
            {
                (true, false) => nameof(Queryable.OrderBy),
                (true, true) => nameof(Queryable.OrderByDescending),
                (false, false) => nameof(Queryable.ThenBy),
                _ => nameof(Queryable.ThenByDescending),
            };
            ordered = Expression.Call(typeof(Queryable), method, [groupType, key.ReturnType], ordered, Expression.Quote(key));
        }

        var rows = Project(source.Provider, ordered, groupType, Row(groupType));
        if (keys.Count == 0)
        {
            return rows;
        }

        rows = skip > 0 ? rows.Skip(skip) : rows;
        return take is { } most ? rows.Take(Math.Max(0, most)) : rows;
    }

    /// <summary>A row per group, to count: unordered and unpaged, with only the first key.</summary>
    public IQueryable<object?[]> Count(IQueryable<TItem> source)
    {
        var (grouped, groupType) = Grouped(source);
        var g = Expression.Parameter(groupType, "g");
        var row = Expression.Lambda(Expression.NewArrayInit(typeof(object), Box(KeyMember(g, 0))), g);
        return Project(source.Provider, grouped.Expression, groupType, row);
    }

    /// <summary>The groups from the rows <see cref="Rows"/> returned. A grand total of nothing still has its one group.</summary>
    public DataGroupResult Read(IReadOnlyList<object?[]> rows, int? total)
    {
        var groups = rows.Select(row =>
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < aggregates.Count; i++)
            {
                values[new DataAggregateField(aggregates[i].Field.Key, aggregates[i].Type).ToString()] = row[keys.Count + 1 + i];
            }

            return new DataGroup(row[..keys.Count], Convert.ToInt32(row[keys.Count], CultureInfo.InvariantCulture)) { Values = values };
        }).ToList();
        return new DataGroupResult(keys.Count == 0 && groups.Count == 0 ? [new DataGroup([], 0)] : groups, total);
    }

    private static IQueryable<object?[]> Project(IQueryProvider provider, Expression source, Type groupType, LambdaExpression row) =>
        provider.CreateQuery<object?[]>(Expression.Call(typeof(Queryable), nameof(Queryable.Select), [groupType, typeof(object?[])], source, Expression.Quote(row)));

    private LambdaExpression Row(Type groupType)
    {
        var g = Expression.Parameter(groupType, "g");
        var cells = new List<Expression>();
        for (var i = 0; i < keys.Count; i++)
        {
            cells.Add(Box(KeyMember(g, i)));
        }

        cells.Add(Box(CountOf(g)));
        cells.AddRange(aggregates.Select(a => Box(Aggregate(a.Field, a.Type, g))));
        return Expression.Lambda(Expression.NewArrayInit(typeof(object), cells), g);
    }

    private static Expression Box(Expression value) => Expression.Convert(value, typeof(object));

    private static Expression KeyMember(ParameterExpression group, int level) =>
        Expression.Property(Expression.Property(group, "Key"), $"K{level}");

    private static MethodCallExpression CountOf(Expression group) =>
        Expression.Call(EnumerableMethods.Count.MakeGenericMethod(typeof(TItem)), group);

    private static Expression Aggregate(DataField<TItem> field, DataAggregate type, ParameterExpression group)
    {
        switch (type)
        {
            case DataAggregate.Count:
                return CountOf(group);
            case DataAggregate.Custom:
                return new Swap(field.AggregateBy!.Parameters[0], group).Visit(field.AggregateBy.Body);
            case DataAggregate.Min or DataAggregate.Max:
                return Expression.Call(EnumerableMethods.MinMax(type == DataAggregate.Min).MakeGenericMethod(typeof(TItem), field.Type), group, field.Value);
            default:
                var selector = Summable(field.Value);
                return Expression.Call(EnumerableMethods.SumAvg(type == DataAggregate.Sum, selector.ReturnType).MakeGenericMethod(typeof(TItem)), group, selector);
        }
    }

    // Enumerable's Sum and Average take int, long, float, double and decimal, or those that can be
    // null: a smaller or unsigned number is widened first.
    private static LambdaExpression Summable(LambdaExpression value)
    {
        var nullable = Nullable.GetUnderlyingType(value.ReturnType) is not null;
        var type = Nullable.GetUnderlyingType(value.ReturnType) ?? value.ReturnType;
        Type? wider = Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 => typeof(int),
            TypeCode.UInt32 => typeof(long),
            TypeCode.UInt64 => typeof(decimal),
            _ => null,
        };
        if (wider is null)
        {
            return value;
        }

        return Expression.Lambda(Expression.Convert(value.Body, nullable ? typeof(Nullable<>).MakeGenericType(wider) : wider), value.Parameters);
    }

    // The source grouped by a key of the fields' values, or by one constant for the grand total.
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Queryable))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Enumerable))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GroupKey<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GroupKey<,>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GroupKey<,,>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GroupKey<,,,>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(IGrouping<,>))]
    private (IQueryable Grouped, Type GroupType) Grouped(IQueryable<TItem> source)
    {
        var x = Expression.Parameter(typeof(TItem), "x");
        LambdaExpression key;
        if (keys.Count == 0)
        {
            key = Expression.Lambda(Expression.Constant(1), x);
        }
        else
        {
            var open = keys.Count switch
            {
                1 => typeof(GroupKey<>),
                2 => typeof(GroupKey<,>),
                3 => typeof(GroupKey<,,>),
                _ => typeof(GroupKey<,,,>),
            };
            var closed = open.MakeGenericType(keys.Select(k => k.Field.Type).ToArray());
            var bindings = keys.Select((k, i) => (MemberBinding)Expression.Bind(closed.GetProperty($"K{i}")!, new Swap(k.Field.Value.Parameters[0], x).Visit(k.Field.Value.Body)));
            key = Expression.Lambda(Expression.MemberInit(Expression.New(closed), bindings), x);
        }

        var grouped = source.Provider.CreateQuery(Expression.Call(typeof(Queryable), nameof(Queryable.GroupBy), [typeof(TItem), key.ReturnType], source.Expression, Expression.Quote(key)));
        return (grouped, typeof(IGrouping<,>).MakeGenericType(key.ReturnType, typeof(TItem)));
    }

    private sealed class Swap(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}

// Enumerable's overloads, found once: a selector's type picks the Sum or Average.
internal static class EnumerableMethods
{
    public static readonly MethodInfo Count = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .First(m => m.Name == nameof(Enumerable.Count) && m.GetParameters().Length == 1);

    public static MethodInfo MinMax(bool min) => typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .First(m => m.Name == (min ? nameof(Enumerable.Min) : nameof(Enumerable.Max)) && m.GetParameters().Length == 2 && m.GetGenericArguments().Length == 2);

    public static MethodInfo SumAvg(bool sum, Type selected) => typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .First(m => m.Name == (sum ? nameof(Enumerable.Sum) : nameof(Enumerable.Average))
            && m.GetParameters().Length == 2
            && m.GetGenericArguments().Length == 1
            && m.GetParameters()[1].ParameterType.GetGenericArguments()[^1] == selected);
}
