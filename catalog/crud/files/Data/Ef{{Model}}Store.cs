using IzzyBlazor;
#if (OverHttp)
using {{StoreNamespace}};
#endif
using Microsoft.EntityFrameworkCore;
#each (Alias in Aliases)
using {{Alias.Name}} = {{Alias.Type}};
#endeach

namespace {{RootNamespace}}.Data;

/// <summary>
/// The {{ModelPluralLower}} in {{Context}}.{{Set}}, from izzy add crud. Search, sort and paging run in the
/// database, so a page of {{ModelPluralLower}} is one query and a count. Each call has a context of its own, so an
/// interactive page, whose services live as long as its circuit, never holds one open or reads stale rows.
/// </summary>
#if (HasFactory)
public sealed class Ef{{Model}}Store(IDbContextFactory<{{Context}}> factory) : I{{Model}}Store
#else
public sealed class Ef{{Model}}Store(IServiceScopeFactory scopes) : I{{Model}}Store
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
#else
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<{{Context}}>();
#endif
#if (HasStatus)
        return await Filter(db.{{Set}}, search, status).CountAsync(cancellationToken);
#else
        return await Filter(db.{{Set}}, search).CountAsync(cancellationToken);
#endif
    }

#if (HasStatus)
    public async ValueTask<DataResult<{{ModelType}}>> FindAsync(DataRequest<{{ModelType}}> request, string? search, {{StatusType}}? status)
#else
    public async ValueTask<DataResult<{{ModelType}}>> FindAsync(DataRequest<{{ModelType}}> request, string? search)
#endif
    {
#if (HasFactory)
        await using var db = await factory.CreateDbContextAsync(request.CancellationToken);
#else
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<{{Context}}>();
#endif
#if (HasStatus)
        var query = Filter(db.{{Set}}.AsNoTracking(), search, status);
#else
        var query = Filter(db.{{Set}}.AsNoTracking(), search);
#endif
#if (OverHttp)
        // A request that came through the API has no grid with it: its keys are read as the model's fields.
        return await request.ToResultAsync(query, DataFields.Of<{{ModelType}}>(), (q, ct) => q.CountAsync(ct), (q, ct) => q.ToListAsync(ct));
#else
        return await request.ToResultAsync(query, (q, ct) => q.CountAsync(ct), (q, ct) => q.ToListAsync(ct));
#endif
    }

    public async Task ExportAsync(Stream output, string? query, CancellationToken cancellationToken = default)
    {
#if (HasFactory)
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
#else
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<{{Context}}>();
#endif
        await DataExport.WriteExcelAsync(output, db.{{Set}}.AsNoTracking(), query, DataFields.Of<{{ModelType}}>(), "{{ModelPlural}}", cancellationToken: cancellationToken);
    }

    public async Task<{{ModelType}}?> GetAsync({{KeyType}} id, CancellationToken cancellationToken = default)
    {
#if (HasFactory)
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
#else
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<{{Context}}>();
#endif
        return await db.{{Set}}.FindAsync([id], cancellationToken);
    }

    public async Task<bool> AddAsync({{ModelType}} item, CancellationToken cancellationToken = default)
    {
#if (HasFactory)
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
#else
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<{{Context}}>();
#endif
#if (KeyIsText)
        if (string.IsNullOrEmpty(item.{{Key}})) item.{{Key}} = Guid.NewGuid().ToString("N");
#endif
#if (KeyIsGuid)
        if (item.{{Key}} == Guid.Empty) item.{{Key}} = Guid.NewGuid();
#endif
        db.{{Set}}.Add(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Its key is taken (two people adding the same one at once), or the database refused a value.
            return false;
        }
    }

    public async Task UpdateAsync({{ModelType}} item, CancellationToken cancellationToken = default)
    {
#if (HasFactory)
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
#else
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<{{Context}}>();
#endif
        db.{{Set}}.Update(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync({{KeyType}} id, CancellationToken cancellationToken = default)
    {
#if (HasFactory)
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
#else
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<{{Context}}>();
#endif
        if (await db.{{Set}}.FindAsync([id], cancellationToken) is not { } item) return true;
        db.{{Set}}.Remove(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else deleted it first.
            return true;
        }
        catch (DbUpdateException)
        {
            // Other records point to it, and the database won't delete them with it.
            return false;
        }
    }

#if (HasStatus)
    private static IQueryable<{{ModelType}}> Filter(IQueryable<{{ModelType}}> query, string? search, {{StatusType}}? status)
#else
    private static IQueryable<{{ModelType}}> Filter(IQueryable<{{ModelType}}> query, string? search)
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
