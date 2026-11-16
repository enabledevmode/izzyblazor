using IzzyBlazor;
using Microsoft.EntityFrameworkCore;
using Customer = global::{{RootNamespace}}.Data.Customer;
using CustomerStatus = global::{{RootNamespace}}.Data.CustomerStatus;
using CustomerPlan = global::{{RootNamespace}}.Data.CustomerPlan;

namespace {{RootNamespace}}.Data;

/// <summary>
/// The customers in ApplicationDbContext.Customers, from izzy add crud. Search, sort and paging run in the
/// database, so a page of customers is one query and a count. Each call has a context of its own, so an
/// interactive page, whose services live as long as its circuit, never holds one open or reads stale rows.
/// </summary>
public sealed class EfCustomerStore(IServiceScopeFactory scopes) : ICustomerStore
{
    public async Task<int> CountAsync(string? search = null, CustomerStatus? status = null, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await Filter(db.Customers, search, status).CountAsync(cancellationToken);
    }

    public async ValueTask<DataResult<Customer>> FindAsync(DataRequest<Customer> request, string? search, CustomerStatus? status)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = Filter(db.Customers.AsNoTracking(), search, status);
        return await request.ToResultAsync(query, (q, ct) => q.CountAsync(ct), (q, ct) => q.ToListAsync(ct));
    }

    public async Task ExportAsync(Stream output, string? query, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DataExport.WriteExcelAsync(output, db.Customers.AsNoTracking(), query, DataFields.Of<Customer>(), "Customers", cancellationToken: cancellationToken);
    }

    public async Task<Customer?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Customers.FindAsync([id], cancellationToken);
    }

    public async Task<bool> AddAsync(Customer item, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Customers.Add(item);
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

    public async Task UpdateAsync(Customer item, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Customers.Update(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.Customers.FindAsync([id], cancellationToken) is not { } item) return true;
        db.Customers.Remove(item);
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

    private static IQueryable<Customer> Filter(IQueryable<Customer> query, string? search, CustomerStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            // LIKE, so case doesn't matter in SQLite or SQL Server; a % or _ typed is matched as written.
            var pattern = "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(x =>
                EF.Functions.Like(x.FirstName, pattern, "\\") ||
                EF.Functions.Like(x.LastName, pattern, "\\") ||
                EF.Functions.Like(x.Email, pattern, "\\"));
        }
        if (status is { } wanted) query = query.Where(x => x.Status == wanted);
        return query;
    }
}
