using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json.Serialization;

namespace IzzyBlazor;

/// <summary>
/// What a data component (a <see cref="Table{TItem}"/>) asks its <c>ItemsProvider</c> for: a range
/// of rows, sorted and filtered, or the page after a cursor. Apply it to a query with
/// <c>ToResultAsync</c> or <see cref="Apply(IQueryable{TItem})"/>; the component's columns
/// say how each key reads an item, so a provider never maps keys to properties itself. It travels:
/// to JSON with <c>System.Text.Json</c>, and to a query string with <see cref="ToQueryString"/>,
/// for a WebAssembly app that calls an API, where <see cref="Apply(IQueryable{TItem}, IReadOnlyList{DataField{TItem}})"/>
/// applies it through the fields the server declares.
/// </summary>
/// <typeparam name="TItem">The type of the items.</typeparam>
public sealed record DataRequest<TItem>
{
    /// <summary>The sort, first key first; empty for the source's own order.</summary>
    public IReadOnlyList<SortField> Sort { get; init; } = [];

    /// <summary>The filter, or null for none.</summary>
    public Filter? Filter { get; init; }

    /// <summary>Free text to look for, in the searchable fields (text, by default); null for none.</summary>
    public string? Search { get; init; }

    /// <summary>Rows to skip: any offset, such as 1,036 for a virtual-scrolling grid.</summary>
    public int Skip { get; init; }

    /// <summary>Rows to take; null for every row.</summary>
    public int? Take { get; init; }

    /// <summary>
    /// When the component pages by cursor (<see cref="DataPaging"/>), the <see cref="DataResult{TItem}.Next"/>
    /// or <see cref="DataResult{TItem}.Previous"/> of an earlier page, as it was given; null for the
    /// first page. <see cref="Skip"/> is then the page's position (rows 401–600 at 200 a page).
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>Whether the component needs the total count. False when it pages by cursor, where the total is optional.</summary>
    public bool NeedsTotal { get; init; } = true;

    /// <summary>Canceled when the component no longer needs this answer (a newer request, or it went away).</summary>
    [JsonIgnore]
    public CancellationToken CancellationToken { get; init; }

    /// <summary>The page, from 1, for a source that thinks in pages: <see cref="Skip"/> / <see cref="Take"/> + 1.</summary>
    [JsonIgnore]
    public int Page => Take is > 0 and var take ? Skip / take + 1 : 1;

    /// <summary>The fields the component attached (its columns), which <see cref="Apply(IQueryable{TItem})"/> uses.</summary>
    internal IReadOnlyList<DataField<TItem>>? Fields { get; init; }

    private IReadOnlyList<DataField<TItem>> AttachedFields => Fields ?? throw new InvalidOperationException(
        $"This request came without its component's fields (over the wire, or made in code): apply it with Apply(source, fields), such as Apply(query, DataFields.Of<{typeof(TItem).Name}>()).");

    /// <summary>Filters, searches, sorts and takes the range of a query, with the component's fields. EF Core translates it.</summary>
    public IQueryable<TItem> Apply(IQueryable<TItem> source) => Apply(source, AttachedFields);

    /// <summary>Keeps what the filter and search match, with the component's fields: count this for the total.</summary>
    public IQueryable<TItem> ApplyFiltering(IQueryable<TItem> source) => ApplyFiltering(source, AttachedFields);

    /// <summary>Sorts a query, with the component's fields.</summary>
    public IQueryable<TItem> ApplySorting(IQueryable<TItem> source) => ApplySorting(source, AttachedFields);

    /// <summary>
    /// Filters, searches, sorts and takes the range of a query, mapping keys through these fields
    /// only: on a server, for a request that came over the wire. A filter on a key that isn't one
    /// of them (or isn't filterable) matches nothing, and a sort by one is left out, so a request
    /// can't reach other properties.
    /// </summary>
    public IQueryable<TItem> Apply(IQueryable<TItem> source, IReadOnlyList<DataField<TItem>> fields) =>
        ApplyRange(ApplySorting(ApplyFiltering(source, fields), fields));

    /// <summary>Keeps what the filter and search match, through these fields only.</summary>
    public IQueryable<TItem> ApplyFiltering(IQueryable<TItem> source, IReadOnlyList<DataField<TItem>> fields)
    {
        if (Filter is { } filter)
        {
            source = FilterExpression.Where(source, filter, fields);
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            source = source.Where(FilterExpression.Search(Search.Trim(), fields));
        }

        return source;
    }

    /// <summary>Sorts a query by the sortable fields named in <see cref="Sort"/>, through these fields only.</summary>
    public IQueryable<TItem> ApplySorting(IQueryable<TItem> source, IReadOnlyList<DataField<TItem>> fields)
    {
        var first = true;
        foreach (var sort in Sort)
        {
            if (fields.FirstOrDefault(f => f.Sortable && string.Equals(f.Key, sort.Key, StringComparison.OrdinalIgnoreCase)) is not { } field)
            {
                continue;
            }

            var key = field.SortValue ?? field.Value;
            var method = (first, sort.Descending) switch
            {
                (true, false) => nameof(Queryable.OrderBy),
                (true, true) => nameof(Queryable.OrderByDescending),
                (false, false) => nameof(Queryable.ThenBy),
                _ => nameof(Queryable.ThenByDescending),
            };
            source = source.Provider.CreateQuery<TItem>(
                Expression.Call(typeof(Queryable), method, [typeof(TItem), key.ReturnType], source.Expression, Expression.Quote(key)));
            first = false;
        }

        return source;
    }

    private IQueryable<TItem> ApplyRange(IQueryable<TItem> source)
    {
        if (Skip > 0)
        {
            source = source.Skip(Skip);
        }

        return Take is { } take ? source.Take(Math.Max(0, take)) : source;
    }

    /// <summary>
    /// The count of what the filter keeps (when <see cref="NeedsTotal"/>) and this range's items,
    /// with the component's fields, from an in-memory list or a synchronous query.
    /// </summary>
    public DataResult<TItem> ToResult(IQueryable<TItem> source) => ToResult(source, AttachedFields);

    /// <summary>The count and items, through these fields only (see <see cref="Apply(IQueryable{TItem}, IReadOnlyList{DataField{TItem}})"/>).</summary>
    public DataResult<TItem> ToResult(IQueryable<TItem> source, IReadOnlyList<DataField<TItem>> fields)
    {
        var filtered = ApplyFiltering(source, fields);
        return new(ApplyRange(ApplySorting(filtered, fields)).ToList(), NeedsTotal ? filtered.Count() : null);
    }

    /// <summary>
    /// The count of what the filter keeps (only when <see cref="NeedsTotal"/>) and this range's
    /// items, with the component's fields, through your async methods, so Core needs no EF Core:
    /// <c>count: (q, ct) =&gt; q.CountAsync(ct), list: (q, ct) =&gt; q.ToListAsync(ct)</c>. Both get the
    /// request's <see cref="CancellationToken"/>.
    /// </summary>
    public Task<DataResult<TItem>> ToResultAsync(IQueryable<TItem> source,
        Func<IQueryable<TItem>, CancellationToken, Task<int>> count,
        Func<IQueryable<TItem>, CancellationToken, Task<List<TItem>>> list) =>
        ToResultAsync(source, AttachedFields, count, list);

    /// <summary>The count and items through your async methods, mapping keys through these fields only.</summary>
    public async Task<DataResult<TItem>> ToResultAsync(IQueryable<TItem> source, IReadOnlyList<DataField<TItem>> fields,
        Func<IQueryable<TItem>, CancellationToken, Task<int>> count,
        Func<IQueryable<TItem>, CancellationToken, Task<List<TItem>>> list)
    {
        var filtered = ApplyFiltering(source, fields);
        int? total = NeedsTotal ? await count(filtered, CancellationToken) : null;
        var items = await list(ApplyRange(ApplySorting(filtered, fields)), CancellationToken);
        return new(items, total);
    }

    /// <summary>
    /// The request as a query string, for a GET API: <c>sort=customer,-total&amp;skip=20&amp;take=10&amp;filter=...&amp;search=...&amp;cursor=...</c>,
    /// with <c>total=false</c> when it doesn't need the total. The filter is in the form
    /// <see cref="Filter.Parse"/> reads (<see cref="FieldFilter.MatchCase"/> isn't written). Read it
    /// back with <see cref="Parse"/>.
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

        Add("sort", Sort.Count == 0 ? null : string.Join(",", Sort.Select(s => s.Descending ? $"-{s.Key}" : s.Key)));
        Add("skip", Skip > 0 ? Skip.ToString(CultureInfo.InvariantCulture) : null);
        Add("take", Take?.ToString(CultureInfo.InvariantCulture));
        Add("filter", Filter?.ToString());
        Add("search", Search);
        Add("cursor", Cursor);
        Add("total", NeedsTotal ? null : "false");
        return string.Join("&", parts);
    }

    /// <summary>
    /// Reads a request from a query string (<see cref="ToQueryString"/>), or a URL with one. A
    /// filter that doesn't parse matches nothing; numbers that don't parse are left out. Apply it
    /// with <see cref="Apply(IQueryable{TItem}, IReadOnlyList{DataField{TItem}})"/>.
    /// </summary>
    public static DataRequest<TItem> Parse(string query)
    {
        var at = query.IndexOf('?', StringComparison.Ordinal);
        var values = QueryString.Parse(at >= 0 ? query[(at + 1)..] : query);
        string? Get(string key) => values.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v.ToString()) ? v.ToString() : null;
        int? Number(string key) => int.TryParse(Get(key), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;

        var filterText = Get("filter");
        return new DataRequest<TItem>
        {
            Sort = SortField.ParseList(Get("sort")),
            Skip = Number("skip") ?? 0,
            Take = Number("take"),
            Filter = filterText is null ? null : IzzyBlazor.Filter.TryParse(filterText, out var filter) ? filter : IzzyBlazor.Filter.Any(),
            Search = Get("search"),
            Cursor = Get("cursor"),
            NeedsTotal = !string.Equals(Get("total"), "false", StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// The request as OData query options, for a provider whose API is an OData endpoint:
    /// <c>$filter=...&amp;$orderby=customer,total desc&amp;$skip=20&amp;$top=10&amp;$count=true&amp;$search=...</c>.
    /// Text that ignores case compares lower-cased (<c>contains(tolower(customer),'ada')</c>), as
    /// OData matches case; field paths use <c>/</c>. A cursor isn't part of OData: pass
    /// <see cref="Cursor"/> (or its <c>$skiptoken</c>) yourself. Values are written as the
    /// component's fields' types, so a Guid or a date that came back from JSON as text is written
    /// as one.
    /// </summary>
    public string ToODataQuery() => ToODataQuery(Fields ?? []);

    /// <summary>The request as OData query options, with values written as these fields' types.</summary>
    public string ToODataQuery(IReadOnlyList<DataField<TItem>> fields)
    {
        var parts = new List<string>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                parts.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }

        Add("$filter", Filter is null ? null : ODataFilter.Write(FilterExpression.TypedFor(Filter, fields), forOData: true));
        Add("$orderby", Sort.Count == 0 ? null : string.Join(",", Sort.Select(s => s.Key.Replace('.', '/') + (s.Descending ? " desc" : ""))));
        Add("$skip", Skip > 0 ? Skip.ToString(CultureInfo.InvariantCulture) : null);
        Add("$top", Take?.ToString(CultureInfo.InvariantCulture));
        Add("$count", NeedsTotal ? "true" : null);
        Add("$search", Search);
        return string.Join("&", parts);
    }

    /// <inheritdoc />
    public bool Equals(DataRequest<TItem>? other) =>
        other is not null && Sort.SequenceEqual(other.Sort) && Equals(Filter, other.Filter) && Search == other.Search
        && Skip == other.Skip && Take == other.Take && Cursor == other.Cursor && NeedsTotal == other.NeedsTotal;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Sort.Count, Filter, Search, Skip, Take, Cursor, NeedsTotal);

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(ToQueryString());
        return true;
    }
}

/// <summary>One key of a sort: a field's key, ascending or descending.</summary>
/// <param name="Key">The field's key, such as <c>"total"</c>.</param>
/// <param name="Descending">Largest, latest or Z first.</param>
public readonly record struct SortField(string Key, bool Descending = false)
{
    /// <summary>The sort as the address writes it: <c>"total"</c>, or <c>"-total"</c> descending.</summary>
    public override string ToString() => Descending ? $"-{Key}" : Key;

    /// <summary>A sort from the address's form: <c>"customer,-total"</c> (a <c>-</c> for descending).</summary>
    public static IReadOnlyList<SortField> ParseList(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(k => k.StartsWith('-') ? new SortField(k[1..], true) : new SortField(k))
                .Where(s => s.Key.Length > 0)
                .ToList();
}

/// <summary>
/// What an <c>ItemsProvider</c> returns: the rows asked for, and the total count or the cursors of
/// the pages around them.
/// </summary>
/// <typeparam name="TItem">The type of the items.</typeparam>
/// <param name="Items">The rows.</param>
/// <param name="Total">
/// How many rows match in all. A Table with numbered pages (<see cref="DataPaging.Pages"/>) needs
/// it; paging by cursor, it's optional and adds "of 1,234" to the range.
/// </param>
public sealed record DataResult<TItem>(IReadOnlyList<TItem> Items, int? Total = null)
{
    /// <summary>The cursor of the page after this one, or null on the last page. Used when the component pages by cursor.</summary>
    public string? Next { get; init; }

    /// <summary>
    /// The cursor of the page before this one, if your data source gives one. Without it a Table
    /// goes back to cursors it has seen on interactive pages; on static pages Previous is
    /// unavailable past the second page (First still works).
    /// </summary>
    public string? Previous { get; init; }
}
