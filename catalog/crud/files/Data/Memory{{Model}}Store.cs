using IzzyBlazor;
#each (Alias in Aliases)
using {{Alias.Name}} = {{Alias.Type}};
#endeach

namespace {{RootNamespace}}.Data;

/// <summary>
/// {{ModelPlural}} kept in memory, from izzy add crud: one list for the whole app, empty when it starts and gone
/// when it stops. For trying the pages out; for real use, add a DbSet&lt;{{Model}}&gt; to your DbContext and
/// run izzy add crud {{Model}} --store ef --overwrite.
/// </summary>
public sealed class Memory{{Model}}Store : I{{Model}}Store
{
    private readonly List<{{ModelType}}> _items = [];
    private readonly Lock _lock = new();

#if (HasStatus)
    public Task<int> CountAsync(string? search = null, {{StatusType}}? status = null, CancellationToken cancellationToken = default)
    {
        lock (_lock) return Task.FromResult(Filter(search, status).Count());
    }

    public ValueTask<DataResult<{{ModelType}}>> FindAsync(DataRequest<{{ModelType}}> request, string? search, {{StatusType}}? status)
    {
        lock (_lock) return ValueTask.FromResult(request.ToResult(Filter(search, status).ToList().AsQueryable()));
    }
#else
    public Task<int> CountAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        lock (_lock) return Task.FromResult(Filter(search).Count());
    }

    public ValueTask<DataResult<{{ModelType}}>> FindAsync(DataRequest<{{ModelType}}> request, string? search)
    {
        lock (_lock) return ValueTask.FromResult(request.ToResult(Filter(search).ToList().AsQueryable()));
    }
#endif

    public Task<{{ModelType}}?> GetAsync({{KeyType}} id, CancellationToken cancellationToken = default)
    {
        lock (_lock) return Task.FromResult(_items.FirstOrDefault(x => x.{{Key}} == id));
    }

    public Task<bool> AddAsync({{ModelType}} item, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
#if (KeyIsGuid)
            if (item.{{Key}} == Guid.Empty) item.{{Key}} = Guid.NewGuid();
#elif (KeyInput)
            if (_items.Any(x => x.{{Key}} == item.{{Key}})) return Task.FromResult(false);
#elif (KeyIsText)
            if (string.IsNullOrEmpty(item.{{Key}})) item.{{Key}} = Guid.NewGuid().ToString("N");
#else
            item.{{Key}} = _items.Count == 0 ? 1 : _items.Max(x => x.{{Key}}) + 1;
#endif
            _items.Add(item);
        }
        return Task.FromResult(true);
    }

    // The pages change the {{SingularLower}} GetAsync gave them, which is the one in the list.
    public Task UpdateAsync({{ModelType}} item, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> DeleteAsync({{KeyType}} id, CancellationToken cancellationToken = default)
    {
        lock (_lock) _items.RemoveAll(x => x.{{Key}} == id);
        return Task.FromResult(true);
    }

#if (HasStatus)
    private IEnumerable<{{ModelType}}> Filter(string? search, {{StatusType}}? status)
#else
    private IEnumerable<{{ModelType}}> Filter(string? search)
#endif
    {
        IEnumerable<{{ModelType}}> found = _items;
#if (HasSearch)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            found = found.Where(x =>
#each (Field in SearchFields)
#if (Field.Last)
                (x.{{Field.Name}}?.Contains(text, StringComparison.CurrentCultureIgnoreCase) ?? false));
#else
                (x.{{Field.Name}}?.Contains(text, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
#endif
#endeach
        }
#endif
#if (HasStatus)
        if (status is { } wanted) found = found.Where(x => x.{{StatusField}} == wanted);
#endif
        return found;
    }
}
