using System.ComponentModel;
using System.Linq.Expressions;
using System.Text.Json;

namespace IzzyBlazor;

/// <summary>
/// The items selected in a <c>Table</c> or <c>ListView</c>: some keys, or every item matching a
/// filter and search except some keys ("Select all 1,234"). Bind it with <c>@bind-Selection</c>, or
/// on a static page with <c>[SupplyParameterFromForm(FormName = "...", Name = "...")]</c> under the
/// view's <c>SelectionName</c>. For a bulk action, <see cref="Apply{TItem}(IQueryable{TItem}, Expression{Func{TItem, string}}, IReadOnlyList{DataField{TItem}})"/>
/// gives the selected items from a query.
/// </summary>
/// <remarks>
/// Treat it as a value: the views hand back a new one on every change. A posted one came from the
/// browser: <see cref="Apply{TItem}(IQueryable{TItem}, Expression{Func{TItem, string}}, IReadOnlyList{DataField{TItem}})"/>
/// only reaches items through the fields it's given, and a filter it can't read matches nothing.
/// Under <see cref="All"/> it doesn't know a view's <c>ItemSelectable</c>: its count and
/// <c>Apply</c> take every matching item, so leave out the ones that can't be selected in the
/// query you apply it to.
/// </remarks>
public sealed class Selection
{
    /// <summary>The selected keys; with <see cref="All"/>, the keys left out.</summary>
    public string[]? Keys { get; set; }

    /// <summary>Every item matching <see cref="Filter"/> and <see cref="Search"/> is selected, except <see cref="Keys"/>.</summary>
    public bool All { get; set; }

    /// <summary>With <see cref="All"/>, the filter it was made under, in the <c>?filter=</c> form; null for none.</summary>
    public string? Filter { get; set; }

    /// <summary>With <see cref="All"/>, the search it was made under; null for none.</summary>
    public string? Search { get; set; }

    /// <summary>
    /// The keys as one JSON list, as the script posts a selection kept across pages: one field
    /// rather than one per key, which stays under the server's limit of 1,024 form values. Added to
    /// <see cref="Keys"/>. For the form post, not for code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? KeyList
    {
        get => null;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            try
            {
                if (JsonSerializer.Deserialize(value, PersistedStateJson.Default.StringArray) is { } keys)
                {
                    Keys = Distinct([.. Keys ?? [], .. keys]);
                }
            }
            catch (JsonException)
            {
                // Not ours: the posted boxes alone.
            }
        }
    }

    /// <summary>Nothing selected.</summary>
    public static Selection None => new();

    /// <summary>These keys selected.</summary>
    public static Selection Of(params IEnumerable<string> keys) => new() { Keys = Distinct(keys) };

    /// <summary>Every item matching the request's filter and search.</summary>
    public static Selection AllOf<TItem>(DataRequest<TItem> request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new() { All = true, Filter = request.Filter?.ToString(), Search = NullIfEmpty(request.Search) };
    }

    /// <summary>Nothing is selected.</summary>
    public bool IsEmpty => !All && (Keys is null || Keys.Length == 0);

    /// <summary>Whether the item with this key is selected (for <see cref="All"/>, among the matching items).</summary>
    public bool Contains(string key) => All != (Keys is { } keys && Array.IndexOf(keys, key) >= 0);

    /// <summary>How many are selected, given how many match; null when <see cref="All"/> and the total isn't known.</summary>
    /// <param name="total">The number of matching items, or null when unknown.</param>
    public int? Count(int? total = null)
    {
        var left = Keys is null ? 0 : Keys.Distinct(StringComparer.Ordinal).Count();
        return !All ? left : total is { } t ? Math.Max(0, t - left) : null;
    }

    /// <summary>A copy with this key selected or cleared.</summary>
    public Selection With(string key, bool selected)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (Contains(key) == selected)
        {
            return this;
        }

        // Under All the keys are the exceptions: selecting one removes it from them.
        var add = selected != All;
        var keys = Keys ?? [];
        return new()
        {
            Keys = add ? [.. keys, key] : Array.FindAll(keys, k => k != key),
            All = All,
            Filter = Filter,
            Search = Search,
        };
    }

    /// <summary>Whether <see cref="All"/> was made under this filter and search; when not, the selection no longer means the same items.</summary>
    public bool MatchesScope(Filter? filter, string? search) =>
        !All || (Filter == filter?.ToString() && NullIfEmpty(Search) == NullIfEmpty(search));

    /// <summary>The selected items of a query, finding items by every field of <typeparamref name="TItem"/> (<see cref="DataFields.Of{TItem}"/>).</summary>
    /// <param name="source">The items.</param>
    /// <param name="key">The view's <c>ItemKey</c>, as an expression, such as <c>o =&gt; o.Id.ToString()</c>.</param>
    public IQueryable<TItem> Apply<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] TItem>(
        IQueryable<TItem> source, Expression<Func<TItem, string>> key) => Apply(source, key, DataFields.Of<TItem>());

    /// <summary>
    /// The selected items of a query: the keys, or what the filter and search match except the keys.
    /// The filter and search reach items through these fields only. EF Core translates it.
    /// </summary>
    /// <param name="source">The items.</param>
    /// <param name="key">The view's <c>ItemKey</c>, as an expression, such as <c>o =&gt; o.Id.ToString()</c>.</param>
    /// <param name="fields">The fields the filter and search may use (the view's columns, or the server's).</param>
    public IQueryable<TItem> Apply<TItem>(IQueryable<TItem> source, Expression<Func<TItem, string>> key, IReadOnlyList<DataField<TItem>> fields)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(fields);
        var keys = Distinct(Keys ?? []);
        if (!All)
        {
            return keys.Length == 0 ? source.Where(_ => false) : source.Where(In(key, keys, negate: false));
        }

        Filter? filter = null;
        if (!string.IsNullOrWhiteSpace(Filter) && !IzzyBlazor.Filter.TryParse(Filter, out filter))
        {
            return source.Where(_ => false);
        }

        var matching = new DataRequest<TItem> { Filter = filter, Search = NullIfEmpty(Search) }.ApplyFiltering(source, fields);
        return keys.Length == 0 ? matching : matching.Where(In(key, keys, negate: true));
    }

    // keys.Contains(key(item)), or its negation. The list is captured as a closure is, so EF Core
    // sends it as one parameter rather than writing every key into the SQL.
    private static Expression<Func<TItem, bool>> In<TItem>(Expression<Func<TItem, string>> key, string[] keys, bool negate)
    {
        var list = new List<string>(keys);
        Expression<Func<string, bool>> contains = k => list.Contains(k);
        var body = new Substitute(contains.Parameters[0], key.Body).Visit(contains.Body);
        return Expression.Lambda<Func<TItem, bool>>(negate ? Expression.Not(body) : body, key.Parameters);
    }

    /// <summary>Whether this is the same selection as another (the same keys, in any order, and scope).</summary>
    internal bool SameAs(Selection? other) =>
        other is not null && All == other.All && Filter == other.Filter && NullIfEmpty(Search) == NullIfEmpty(other.Search)
        && new HashSet<string>(Keys ?? [], StringComparer.Ordinal).SetEquals(other.Keys ?? []);

    private sealed class Substitute(ParameterExpression parameter, Expression with) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == parameter ? with : node;
    }

    private static string[] Distinct(IEnumerable<string> keys) => [.. keys.Where(k => k is not null).Distinct(StringComparer.Ordinal)];

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
