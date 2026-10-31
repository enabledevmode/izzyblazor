using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace IzzyBlazor;

/// <summary>
/// Asks for the values a column's filter can list: a page of them, narrowed by what's typed in the
/// list's box and by the other filters. Values are strings in the invariant form, so a request can
/// travel: <see cref="ToQueryString"/> and <see cref="Parse"/> take it to an API and back.
/// </summary>
public sealed record FilterValuesRequest
{
    private const int MaxTake = 500;

    /// <summary>The column's key.</summary>
    public required string Field { get; init; }

    /// <summary>What's typed in the list's box.</summary>
    public string? Search { get; init; }

    /// <summary>How many values to skip.</summary>
    public int Skip { get; init; }

    /// <summary>How many values to take, at most 500.</summary>
    public int Take { get; init; } = 50;

    /// <summary>Every condition but this column's, so the list shows what the others leave. Pass it on or ignore it.</summary>
    public Filter? OtherFilters { get; init; }

    /// <summary>The grid's search.</summary>
    public string? GridSearch { get; init; }

    /// <summary>The values ticked, which stay listed whatever the other filters leave. Null is the blank.</summary>
    public IReadOnlyList<string?> Ticked { get; init; } = [];

    /// <summary>The values unticked, which stay listed, to be ticked again. Null is the blank.</summary>
    public IReadOnlyList<string?> Unticked { get; init; } = [];

    /// <summary>Canceled when the list no longer wants the answer: another key was typed, or it closed.</summary>
    [JsonIgnore]
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// The request as a query string, for a GET API:
    /// <c>field=customer&amp;search=ad&amp;skip=50&amp;filter=...&amp;ticked=Ada&amp;ticked=Grace</c>. Read it back with <see cref="Parse"/>.
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

        void AddList(string key, IReadOnlyList<string?> values)
        {
            foreach (var value in values)
            {
                Add(key, value);
            }

            Add($"{key}-blank", values.Contains(null) ? "1" : null);
        }

        Add("field", Field);
        Add("search", Search);
        Add("skip", Skip > 0 ? Skip.ToString(CultureInfo.InvariantCulture) : null);
        Add("take", Take != 50 ? Take.ToString(CultureInfo.InvariantCulture) : null);
        Add("filter", OtherFilters?.ToString());
        Add("gridsearch", GridSearch);
        AddList("ticked", Ticked);
        AddList("unticked", Unticked);
        return string.Join("&", parts);
    }

    /// <summary>
    /// Reads a request from a query string (<see cref="ToQueryString"/>), or a URL with one. A filter
    /// that doesn't parse matches nothing; numbers that don't parse are left out.
    /// </summary>
    public static FilterValuesRequest Parse(string query)
    {
        var at = query.IndexOf('?', StringComparison.Ordinal);
        var values = QueryString.Parse(at >= 0 ? query[(at + 1)..] : query);
        string? Get(string key) => values.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v.ToString()) ? v.ToString() : null;
        int? Number(string key) => int.TryParse(Get(key), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
        List<string?> List(string key)
        {
            List<string?> list = values.TryGetValue(key, out var v) ? [.. v.Where(s => !string.IsNullOrEmpty(s))] : [];
            if (Get($"{key}-blank") is not null)
            {
                list.Add(null);
            }

            return list;
        }

        var filterText = Get("filter");
        return new FilterValuesRequest
        {
            Field = Get("field") ?? "",
            Search = Get("search"),
            Skip = Number("skip") ?? 0,
            Take = Number("take") ?? 50,
            OtherFilters = filterText is null ? null : Filter.TryParse(filterText, out var filter) ? filter : Filter.Any(),
            GridSearch = Get("gridsearch"),
            Ticked = List("ticked"),
            Unticked = List("unticked"),
        };
    }

    /// <summary>
    /// The field's distinct values, through these fields only, from an in-memory list or a synchronous
    /// query: the other filters and the grid's search, then the search as a contains, sorted, with what's
    /// ticked or unticked first on the first page. A field that isn't one of them, or isn't filterable,
    /// gives nothing. Works on text fields; for others, write a <see cref="FilterValuesLoader"/>.
    /// </summary>
    public FilterValuesResult ToResult<TItem>(IQueryable<TItem> source, IReadOnlyList<DataField<TItem>> fields) =>
        Values(source, fields) is var (query, first) && query is not null ? Finish(first, query.ToList()) : FilterValuesResult.Empty;

    /// <summary>
    /// As <see cref="ToResult"/>, through your async method, so Core needs no EF Core:
    /// <c>(q, ct) =&gt; q.ToListAsync(ct)</c>. It gets the request's <see cref="CancellationToken"/>.
    /// </summary>
    public async ValueTask<FilterValuesResult> ToResultAsync<TItem>(IQueryable<TItem> source, IReadOnlyList<DataField<TItem>> fields,
        Func<IQueryable<string?>, CancellationToken, Task<List<string?>>> list) =>
        Values(source, fields) is var (query, first) && query is not null ? Finish(first, await list(query, CancellationToken)) : FilterValuesResult.Empty;

    private int EffectiveTake => Math.Clamp(Take, 1, MaxTake);

    // What must stay listed, of what a search leaves: without a search, a value's place doesn't depend on the rows.
    private string?[] Kept(FilterCase matching)
    {
        var search = Search?.Trim();
        return Ticked.Concat(Unticked).Distinct()
            .Where(v => string.IsNullOrEmpty(search) || (v is not null && v.Contains(search, matching == FilterCase.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
            .ToArray();
    }

    // The query for this page, and what comes ahead of its answer: what must stay listed, on the first page.
    private (IQueryable<string?>? Query, string?[] First) Values<TItem>(IQueryable<TItem> source, IReadOnlyList<DataField<TItem>> fields)
    {
        if (fields.FirstOrDefault(f => f.Filterable && string.Equals(f.Key, Field, StringComparison.OrdinalIgnoreCase)) is not { } field)
        {
            return (null, []);
        }

        if (field.Type != typeof(string))
        {
            throw new InvalidOperationException(
                $"Listing the values of \"{field.Key}\", a {field.Type.Name}, takes a {nameof(FilterValuesLoader)} of your own: {nameof(ToResult)} reads text fields.");
        }

        if (OtherFilters is { } others)
        {
            source = FilterExpression.Where(source, others, fields);
        }

        if (!string.IsNullOrWhiteSpace(GridSearch))
        {
            source = source.Where(FilterExpression.Search(GridSearch.Trim(), fields));
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            // On a field of its own, as a column's FilterOperators limit people's filters, not this.
            DataField<TItem>[] own = [new() { Key = field.Key, Value = field.Value, Filterable = true, Case = field.Case }];
            source = FilterExpression.Where(source, new FieldFilter(field.Key, FilterOperator.Contains, Search.Trim()), own);
        }

        // Empty text and no text are one row, as the blank filter treats them.
        // A list, not an array: under C# 14 an array's Contains binds to a span, which not every provider can translate.
        var kept = Kept(field.Case).ToList();
        var values = source.Provider.CreateQuery<string?>(
                Expression.Call(typeof(Queryable), nameof(Queryable.Select), [typeof(TItem), typeof(string)], source.Expression, Expression.Quote(field.Value)))
            .Select(v => v == "" ? null : v)
            .Distinct();
        if (kept.Count > 0)
        {
            values = values.Where(v => !kept.Contains(v));
        }

        values = values.OrderBy(v => v);

        // The kept ones are the first rows, and take their room from the page they fall on.
        var skip = Math.Max(0, Skip);
        var first = kept.Skip(skip).ToArray();
        return (values.Skip(Math.Max(0, skip - kept.Count)).Take(Math.Max(0, EffectiveTake + 1 - first.Length)), first);
    }

    private FilterValuesResult Finish(string?[] first, List<string?> found)
    {
        var rows = first.Concat(found).Select(v => new FilterValue(v)).ToList();
        var more = rows.Count > EffectiveTake;
        return new(more ? rows.GetRange(0, EffectiveTake) : rows, more);
    }

    /// <summary>A hand-written loader's answer with what must stay listed, which it left out, at the start.</summary>
    internal FilterValuesResult Keeping(FilterValuesResult result, FilterCase matching)
    {
        if (Skip > 0)
        {
            return result;
        }

        var missing = Kept(matching).Where(v => !result.Values.Any(r => r.Value == v)).Select(v => new FilterValue(v)).ToList();
        return missing.Count == 0 ? result : result with { Values = [.. missing, .. result.Values] };
    }
}

/// <summary>A value a column's filter can list.</summary>
/// <param name="Value">The value, in the invariant form. Null is the blank row.</param>
/// <param name="Text">What it shows, such as a name for an id. Defaults to the value.</param>
public sealed record FilterValue(string? Value, string? Text = null);

/// <summary>A page of the values a column's filter can list.</summary>
/// <param name="Values">The values, in the order to show them.</param>
/// <param name="HasMore">Whether there are more after these.</param>
public sealed record FilterValuesResult(IReadOnlyList<FilterValue> Values, bool HasMore = false)
{
    internal static readonly FilterValuesResult Empty = new([]);
}

/// <summary>Finds the values a column's filter can list. It must keep what's ticked listed; the library adds any it leaves out.</summary>
public delegate ValueTask<FilterValuesResult> FilterValuesLoader(FilterValuesRequest request);
