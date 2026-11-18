using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace {{RootNamespace}}.Data;

/// <summary>
/// One entry in the audit log: who did what to which record, when and from where. Written as changes are saved
/// (Auditing/AuditInterceptor.cs), and by the actions that aren't a record's fields changing, such as a sign-in.
/// </summary>
public class AuditEvent
{
    public long Id { get; set; }

    /// <summary>When it happened, in UTC.</summary>
    public DateTime Time { get; set; }

    /// <summary>The user who did it; null for the app itself (the seeder) or a sign-in to an unknown account.</summary>
    [StringLength(450)] public string? ActorId { get; set; }

    /// <summary>Their name or email as it was then, so the entry still reads after they're deleted.</summary>
    [StringLength(256)] public string? ActorName { get; set; }

    /// <summary>What happened: one of <see cref="AuditActions"/>.</summary>
    [Required, StringLength(40)] public string Action { get; set; } = "";

    /// <summary>The kind of record, such as Contact; null for a sign-in.</summary>
    [StringLength(60)] public string? EntityType { get; set; }

    [StringLength(60)] public string? EntityId { get; set; }

    /// <summary>The record's name as it was then, such as a contact's name or a deal's.</summary>
    [StringLength(200)] public string? EntityName { get; set; }

    /// <summary>The fields that changed, as JSON: see <see cref="ReadChanges"/>.</summary>
    public string? Changes { get; set; }

    /// <summary>More about it, such as why a sign-in failed.</summary>
    [StringLength(500)] public string? Note { get; set; }

    [StringLength(45)] public string? IpAddress { get; set; }

    public IReadOnlyList<AuditChange> ReadChanges() =>
        Changes is null ? [] : JsonSerializer.Deserialize<List<AuditChange>>(Changes) ?? [];

    public static string? WriteChanges(IReadOnlyList<AuditChange> changes) =>
        changes.Count == 0 ? null : JsonSerializer.Serialize(changes);
}

/// <summary>One field's change: its value before (null when created) and after (null when deleted).</summary>
public sealed record AuditChange(string Field, string? Before, string? After);

/// <summary>The actions the audit log records.</summary>
public static class AuditActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string SignedIn = "SignedIn";
    public const string SignInFailed = "SignInFailed";
    public const string Invited = "Invited";
    public const string RolesChanged = "RolesChanged";
    public const string Deactivated = "Deactivated";
    public const string Reactivated = "Reactivated";
    public const string Unlocked = "Unlocked";
    public const string TwoFactorReset = "TwoFactorReset";
    public const string PasswordResetSent = "PasswordResetSent";
    public const string StageChanged = "StageChanged";
    public const string Completed = "Completed";
}
