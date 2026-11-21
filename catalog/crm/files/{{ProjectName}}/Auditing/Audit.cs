using System.Security.Claims;
using {{RootNamespace}}.Data;

namespace {{RootNamespace}}.Auditing;

/// <summary>
/// Writes the audit log's entries that aren't a record's fields changing, such as a user deactivated or a deal
/// moved to another stage. Changes to audited records are written as they're saved (<see cref="AuditInterceptor"/>), with no call here.
/// </summary>
public sealed class Audit(ApplicationDbContext db, IHttpContextAccessor http)
{
    /// <summary>Records <paramref name="action"/>, done by the signed-in user unless <paramref name="actor"/> is given.</summary>
    /// <param name="action">One of <see cref="AuditActions"/>.</param>
    /// <param name="type">The kind of record it was done to, such as User; null for none.</param>
    /// <param name="id">That record's id.</param>
    /// <param name="name">How to name that record, such as the user's email.</param>
    /// <param name="changes">Fields it changed, if any, such as a user's roles.</param>
    /// <param name="note">More about it, such as why a login failed.</param>
    /// <param name="actor">Who did it, when they aren't the signed-in user (they're logging in).</param>
    public async Task RecordAsync(string action, string? type = null, string? id = null, string? name = null,
        IReadOnlyList<AuditChange>? changes = null, string? note = null, ApplicationUser? actor = null)
    {
        var context = http.HttpContext;
        var actorId = actor?.Id ?? context?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        // Their name as the app shows it, not the login name (their email).
        actor ??= actorId is null ? null : await db.Users.FindAsync(actorId);
        db.AuditEvents.Add(new AuditEvent
        {
            Time = DateTime.UtcNow,
            ActorId = actorId,
            ActorName = actor?.Name ?? actor?.Email ?? context?.User.Identity?.Name,
            Action = action,
            EntityType = type,
            EntityId = id,
            EntityName = name,
            Changes = changes is null ? null : AuditEvent.WriteChanges(changes),
            Note = note,
            IpAddress = context?.Connection.RemoteIpAddress?.ToString(),
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Moves a tracked <paramref name="deal"/> to another stage and saves it, as one line of its own in the log
    /// (not an ordinary change). Won or Lost closes it on <paramref name="today"/>; back in the pipeline, it's
    /// open again. Why it was lost (Deal.LostReason) is the caller's to set, and goes when the deal isn't lost.
    /// </summary>
    public async Task MoveAsync(Deal deal, DealStage to, DateOnly today)
    {
        var from = deal.Stage;
        deal.Stage = to;
        if (to != DealStage.Lost) deal.LostReason = null;
        deal.Closed = deal.IsOpen ? null : today;
        await db.SaveUnauditedAsync();
        await RecordAsync(AuditActions.StageChanged, nameof(Deal), deal.Id.ToString(), deal.Name, [new AuditChange("Stage", from.ToString(), to.ToString())]);
    }
}
