using System.Globalization;
using System.Linq.Expressions;

namespace IzzyBlazor;

// A TreeList's request for one level of rows: the children of Parent, sorted, paged, and while
// filtering, only the ones with a match at or under them.
public sealed partial record DataRequest<TItem>
{
    private const int MaxTreeDepth = 64;

    /// <summary>
    /// One level of a tree, for a <see cref="TreeList{TItem}"/>'s <c>ItemsProvider</c>: the rows whose
    /// parent is <see cref="Parent"/> (the top rows when it's null), sorted and paged, with the total
    /// count. While filtering, it keeps the rows that match or have a match under them, and with
    /// <see cref="FilterBranch"/> the rows under a match too. Give it the key and parent key as
    /// properties, such as <c>e =&gt; e.Id, e =&gt; e.ManagerId</c>. From an in-memory list or a
    /// synchronous query.
    /// </summary>
    /// <remarks>While filtering, it loads the matching rows and the rows above them, a level at a time.</remarks>
    public DataResult<TItem> ToTreeResult<TKey, TParent>(IQueryable<TItem> source, Expression<Func<TItem, TKey>> key, Expression<Func<TItem, TParent>> parent)
        where TKey : notnull =>
        TreeResultAsync(source, AttachedFields, key, parent, (q, _) => Task.FromResult(q.Count()), (q, _) => Task.FromResult(q.ToList())).GetAwaiter().GetResult();

    /// <summary>
    /// One level of a tree (see <see cref="ToTreeResult"/>) through your async methods, so Core needs
    /// no EF Core: <c>count: (q, ct) =&gt; q.CountAsync(ct), list: (q, ct) =&gt; q.ToListAsync(ct)</c>.
    /// </summary>
    public Task<DataResult<TItem>> ToTreeResultAsync<TKey, TParent>(IQueryable<TItem> source,
        Expression<Func<TItem, TKey>> key, Expression<Func<TItem, TParent>> parent,
        Func<IQueryable<TItem>, CancellationToken, Task<int>> count,
        Func<IQueryable<TItem>, CancellationToken, Task<List<TItem>>> list)
        where TKey : notnull =>
        TreeResultAsync(source, AttachedFields, key, parent, count, list);

    private async Task<DataResult<TItem>> TreeResultAsync<TKey, TParent>(IQueryable<TItem> source, IReadOnlyList<DataField<TItem>> fields,
        Expression<Func<TItem, TKey>> key, Expression<Func<TItem, TParent>> parent,
        Func<IQueryable<TItem>, CancellationToken, Task<int>> count,
        Func<IQueryable<TItem>, CancellationToken, Task<List<TItem>>> list)
        where TKey : notnull
    {
        var parentValue = Parent is null ? default : ParseKey<TParent>(Parent);
        if (Filter is null && string.IsNullOrWhiteSpace(Search))
        {
            var level = source.Where(Is(parent, parentValue));
            int? total = NeedsTotal ? await count(level, CancellationToken) : null;
            return new(await list(ApplyRange(ApplySorting(level, fields)), CancellationToken), total);
        }

        // The matches, then the rows above them a level at a time, and with FilterBranch the rows under them.
        var keyOf = key.Compile();
        var parentOf = parent.Compile();
        var kept = new Dictionary<TKey, TItem>();
        var matches = await list(ApplyFiltering(source, fields), CancellationToken);
        foreach (var item in matches)
        {
            kept.TryAdd(keyOf(item), item);
        }

        var up = Above(matches);
        for (var depth = 0; up.Count > 0 && depth < MaxTreeDepth; depth++)
        {
            var found = await list(source.Where(In(key, up)), CancellationToken);
            foreach (var item in found)
            {
                kept.TryAdd(keyOf(item), item);
            }

            up = Above(found);
        }

        if (FilterBranch)
        {
            var down = matches.Select(keyOf).Distinct().ToList();
            for (var depth = 0; down.Count > 0 && depth < MaxTreeDepth; depth++)
            {
                var found = await list(source.Where(In(parent, down.Select(k => (TParent)(object)k!))), CancellationToken);
                down = found.Where(i => kept.TryAdd(keyOf(i), i)).Select(keyOf).ToList();
            }
        }

        var atLevel = kept.Values.Where(i => EqualityComparer<TParent>.Default.Equals(parentOf(i), parentValue)).ToList();
        var page = ApplyRange(ApplySorting(DataViewBase<TItem>.AsQuery(atLevel), fields)).ToList();
        return new(page, atLevel.Count);

        // The parents of these rows not kept yet.
        List<TKey> Above(IEnumerable<TItem> items) =>
            items.Select(parentOf).Where(p => p is not null).Select(p => (TKey)(object)p!).Where(k => !kept.ContainsKey(k)).Distinct().ToList();
    }

    // A key from the tree list's text: "42" for an int column, as ItemKey wrote it.
    internal static T ParseKey<T>(string text)
    {
        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        try
        {
            object value = type == typeof(string) ? text
                : type == typeof(Guid) ? Guid.Parse(text)
                : Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
            return (T)value;
        }
        catch (Exception e) when (e is FormatException or OverflowException or ArgumentException or InvalidCastException)
        {
            throw new InvalidOperationException($"The tree's parent key \"{text}\" isn't a {type.Name}: give the tree the properties ItemKey writes.", e);
        }
    }

    internal static Expression<Func<TItem, bool>> Is<T>(Expression<Func<TItem, T>> property, T? value) =>
        Expression.Lambda<Func<TItem, bool>>(Expression.Equal(property.Body, Expression.Constant(value, typeof(T))), property.Parameters);

    internal static Expression<Func<TItem, bool>> In<T>(Expression<Func<TItem, T>> property, IEnumerable<T> values) =>
        Expression.Lambda<Func<TItem, bool>>(
            Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(T)], Expression.Constant(values.ToList()), property.Body),
            property.Parameters);
}
