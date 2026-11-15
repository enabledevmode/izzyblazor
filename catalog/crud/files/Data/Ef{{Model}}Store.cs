using IzzyBlazor;
using Microsoft.EntityFrameworkCore;
#if (HasModelNamespace)
using {{ModelNamespace}};
#endif
#if (HasContextNamespace)
using {{ContextNamespace}};
#endif

namespace {{RootNamespace}}.Data;

/// <summary>
/// The {{ModelPluralLower}} in {{Context}}.{{Set}}, from izzy add crud. Search, sort and paging run in the
/// database, so a page of {{ModelPluralLower}} is one query and a count.
/// </summary>
#if (HasFactory)
public sealed class Ef{{Model}}Store(IDbContextFactory<{{Context}}> factory) : I{{Model}}Store
#else
public sealed class Ef{{Model}}Store({{Context}} db) : I{{Model}}Store
#endif
{
#if (HasStatus)
    public async Task<int> CountAsync(string? search = null, {{StatusType}}? status = null, CancellationToken cancellationToken = default)
#else
    public async Task<int> CountAsync(string? search = null, CancellationToken cancellationToken = default)
#endif
    {
#if (HasFactory)
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
#endif
#if (HasStatus)
        return await Filter(db.{{Set}}, search, status).CountAsync(cancellationToken);
#else
        return await Filter(db.{{Set}}, search).CountAsync(cancellationToken);
#endif
    }

#if (HasStatus)
    public async ValueTask<DataResult<{{Model}}>> FindAsync(DataRequest<{{Model}}> request, string? search, {{StatusType}}? status)
#else
    public async ValueTask<DataResult<{{Model}}>> FindAsync(DataRequest<{{Model}}> request, string? search)
#endif
    {
#if (HasFactory)
        await using var db = await factory.CreateDbContextAsync(request.CancellationToken);
#endif
#if (HasStatus)
        var query = Filter(db.{{Set}}.AsNoTracking(), search, status);
#else
        var query = Filter(db.{{Set}}.AsNoTracking(), search);
#endif
        return await request.ToResultAsync(query, (q, ct) => q.CountAsync(ct), (q, ct) => q.ToListAsync(ct));
    }

    public async Task<{{Model}}?> GetAsync({{KeyType}} id, CancellationToken cancellationToken = default)
    {
#if (HasFactory)
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
#endif
        return await db.{{Set}}.FindAsync([id], cancellationToken);
    }

    public async Task AddAsync({{Model}} item, CancellationToken cancellationToken = default)
    {
#if (HasFactory)
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
#endif
#if (KeyIsText)
        if (string.IsNullOrEmpty(item.{{Key}})) item.{{Key}} = Guid.NewGuid().ToString("N");
#endif
        db.{{Set}}.Add(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync({{Model}} item, CancellationToken cancellationToken = default)
    {
#if (HasFactory)
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
#endif
        db.{{Set}}.Update(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync({{KeyType}} id, CancellationToken cancellationToken = default)
    {
#if (HasFactory)
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
#endif
        if (await db.{{Set}}.FindAsync([id], cancellationToken) is not { } item) return true;
        db.{{Set}}.Remove(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Other records point to it, and the database won't delete them with it.
            return false;
        }
    }

#if (HasStatus)
    private static IQueryable<{{Model}}> Filter(IQueryable<{{Model}}> query, string? search, {{StatusType}}? status)
#else
    private static IQueryable<{{Model}}> Filter(IQueryable<{{Model}}> query, string? search)
#endif
    {
#if (HasSearch)
        if (!string.IsNullOrWhiteSpace(search))
        {
            // LIKE, so case doesn't matter in SQLite or SQL Server; a % or _ typed is matched as written.
            var pattern = "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(x =>
#each (Field in SearchFields)
#if (Field.Last)
                EF.Functions.Like(x.{{Field.Name}}, pattern, "\\"));
#else
                EF.Functions.Like(x.{{Field.Name}}, pattern, "\\") ||
#endif
#endeach
        }
#endif
#if (HasStatus)
        if (status is { } wanted) query = query.Where(x => x.{{StatusField}} == wanted);
#endif
        return query;
    }
}
