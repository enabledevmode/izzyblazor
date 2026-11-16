using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using {{RootNamespace}}.Data;

namespace {{RootNamespace}}.Auditing;

/// <summary>A record whose changes go in the audit log: add this to an entity to audit it.</summary>
public interface IAudited
{
    /// <summary>How the log names the record, such as a customer's name; kept as it was when it changed.</summary>
    string AuditName { get; }
}

/// <summary>
/// Writes an <see cref="AuditEvent"/> for every <see cref="IAudited"/> record a save creates, changes or deletes,
/// with each changed field's value before and after. The values are read before the save; the events are written
/// after it, once new records have their ids.
/// </summary>
public sealed class AuditInterceptor(IHttpContextAccessor http) : SaveChangesInterceptor
{
    // The changes a save is making, by context, from before the save to after it.
    readonly ConditionalWeakTable<DbContext, List<Pending>> _pending = [];

    sealed record Pending(EntityEntry Entry, AuditEvent Event);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (Finish(eventData.Context)) eventData.Context!.SaveChanges();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (Finish(eventData.Context)) await eventData.Context!.SaveChangesAsync(cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pending.Remove(eventData.Context!);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending.Remove(eventData.Context!);
        return Task.CompletedTask;
    }

    void Capture(DbContext? context)
    {
        if (context is not ApplicationDbContext { Auditing: true }) return;
        var user = http.HttpContext?.User;
        var ip = http.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var now = DateTime.UtcNow;
        var pending = new List<Pending>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not IAudited audited) continue;
            var (action, changes) = entry.State switch
            {
                EntityState.Added => (AuditActions.Created, Fields(entry, p => !p.Metadata.IsPrimaryKey(), p => null, p => p.CurrentValue)),
                EntityState.Deleted => (AuditActions.Deleted, Fields(entry, p => !p.Metadata.IsPrimaryKey(), p => p.OriginalValue, p => null)),
                EntityState.Modified => (AuditActions.Updated, Fields(entry, p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue), p => p.OriginalValue, p => p.CurrentValue)),
                _ => (null, []),
            };
            if (action is null || (action == AuditActions.Updated && changes.Count == 0)) continue;
            pending.Add(new Pending(entry, new AuditEvent
            {
                Time = now,
                ActorId = user?.FindFirstValue(ClaimTypes.NameIdentifier),
                ActorName = user?.Identity?.Name,
                Action = action,
                EntityType = entry.Metadata.ClrType.Name,
                EntityName = audited.AuditName,
                Changes = AuditEvent.WriteChanges(changes),
                IpAddress = ip,
            }));
        }
        if (pending.Count > 0) _pending.AddOrUpdate(context, pending);
    }

    // Whether there are events to write: then the caller saves them.
    bool Finish(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending)) return false;
        _pending.Remove(context);
        foreach (var (entry, audit) in pending)
        {
            // A new record's id is only known now; a deleted one's was read before the save.
            audit.EntityId = Format(entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue);
            context.Add(audit);
        }
        return true;
    }

    static List<AuditChange> Fields(EntityEntry entry, Func<PropertyEntry, bool> include, Func<PropertyEntry, object?> before, Func<PropertyEntry, object?> after) =>
        [.. entry.Properties.Where(include).Select(p => new AuditChange(p.Metadata.Name, Format(before(p)), Format(after(p))))];

    // Values as text that reads the same in any culture: dates as 2026-11-16, numbers with a dot.
    static string? Format(object? value) => value switch
    {
        null => null,
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime t => t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
