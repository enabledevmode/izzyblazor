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
    /// <summary>How the log names the record, such as a contact's name; kept as it was when it changed.</summary>
    string AuditName { get; }
}

/// <summary>A field the log leaves out: one the app keeps up itself, such as when a contact was last heard from.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotAuditedAttribute : Attribute;

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
        var actorId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        string? actorName = null;
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is not IAudited audited) continue;
            var (action, changes) = entry.State switch
            {
                EntityState.Added => (AuditActions.Created, Fields(entry, p => !p.Metadata.IsPrimaryKey(), p => null, p => p.CurrentValue)),
                EntityState.Deleted => (AuditActions.Deleted, Fields(entry, p => !p.Metadata.IsPrimaryKey(), p => p.OriginalValue, p => null)),
                EntityState.Modified => (AuditActions.Updated, Updated(entry)),
                _ => (null, []),
            };
            if (action is null || (action == AuditActions.Updated && changes.Count == 0)) continue;
            // Their name as the app shows it, not the sign-in name (their email), read once per save.
            actorName ??= actorId is null ? null
                : ((ApplicationDbContext)context).Users.AsNoTracking().Where(u => u.Id == actorId).Select(u => u.Name ?? u.Email).FirstOrDefault()
                  ?? user?.Identity?.Name;
            pending.Add(new Pending(entry, new AuditEvent
            {
                Time = now,
                ActorId = actorId,
                ActorName = actorName,
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

    // What an update changes. A record saved through Update() (a copy read without tracking)
    // has no original values, only the new ones; then the database's values are the "before".
    static List<AuditChange> Updated(EntityEntry entry)
    {
        var tracked = Fields(entry, p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue), p => p.OriginalValue, p => p.CurrentValue);
        if (tracked.Count > 0 || entry.GetDatabaseValues() is not { } saved) return tracked;
        return Fields(entry, p => !p.Metadata.IsPrimaryKey() && !Equals(saved[p.Metadata], p.CurrentValue), p => saved[p.Metadata], p => p.CurrentValue);
    }

    static List<AuditChange> Fields(EntityEntry entry, Func<PropertyEntry, bool> include, Func<PropertyEntry, object?> before, Func<PropertyEntry, object?> after) =>
        [.. entry.Properties.Where(p => include(p) && p.Metadata.PropertyInfo?.IsDefined(typeof(NotAuditedAttribute), false) != true).Select(p => new AuditChange(p.Metadata.Name, Format(before(p)), Format(after(p))))];

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
