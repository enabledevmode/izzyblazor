using IzzyBlazor;
#each (Alias in Aliases)
using {{Alias.Name}} = {{Alias.Type}};
#endeach

namespace {{StoreNamespace}};

/// <summary>
/// Where the {{ModelPluralLower}} pages read and write {{ModelPluralLower}}, from izzy add crud. The pages ask only this,
/// so moving the {{ModelPluralLower}} to another database or an API means a new store, not new pages.
/// </summary>
public interface I{{Model}}Store
{
#if (HasStatus)
    /// <summary>How many {{ModelPluralLower}} the list's filters keep; with none, how many there are.</summary>
    Task<int> CountAsync(string? search = null, {{StatusType}}? status = null, CancellationToken cancellationToken = default);
#else
    /// <summary>How many {{ModelPluralLower}} the list's search keeps; with none, how many there are.</summary>
    Task<int> CountAsync(string? search = null, CancellationToken cancellationToken = default);
#endif

#if (HasStatus)
    /// <summary>
    /// The page of {{ModelPluralLower}} the grid asks for, sorted as it asks, after the list's own filters:
    /// text to look for (<paramref name="search"/>), and a {{StatusLabel}}.
    /// </summary>
    ValueTask<DataResult<{{ModelType}}>> FindAsync(DataRequest<{{ModelType}}> request, string? search, {{StatusType}}? status);
#else
    /// <summary>
    /// The page of {{ModelPluralLower}} the grid asks for, sorted as it asks, after the list's own search
    /// (<paramref name="search"/>), when it has one.
    /// </summary>
    ValueTask<DataResult<{{ModelType}}>> FindAsync(DataRequest<{{ModelType}}> request, string? search);
#endif

    /// <summary>
    /// Writes the {{ModelPluralLower}} to an Excel file, as the grid's address asks (<paramref name="query"/>: its search,
    /// sort and columns).
    /// </summary>
    Task ExportAsync(Stream output, string? query, CancellationToken cancellationToken = default);

    /// <summary>The {{SingularLower}} with this {{Key}}, or null when there's none.</summary>
    Task<{{ModelType}}?> GetAsync({{KeyType}} id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a {{SingularLower}}, giving it its {{Key}} when it has none. False when it can't be added, such as when
    /// its {{Key}} is taken.
    /// </summary>
    Task<bool> AddAsync({{ModelType}} item, CancellationToken cancellationToken = default);

    /// <summary>Saves a {{SingularLower}} read with <see cref="GetAsync"/> and changed.</summary>
    Task UpdateAsync({{ModelType}} item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the {{SingularLower}} with this {{Key}}. False when it can't be, because other records point to it.
    /// </summary>
    Task<bool> DeleteAsync({{KeyType}} id, CancellationToken cancellationToken = default);
}
