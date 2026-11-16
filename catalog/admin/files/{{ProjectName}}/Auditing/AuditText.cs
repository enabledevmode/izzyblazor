using {{RootNamespace}}.Data;

namespace {{RootNamespace}}.Auditing;

/// <summary>How the audit log's pages word and color each action: the Audit log grid and a record's history.</summary>
public static class AuditText
{
    /// <summary>Every action, in the order the Action filter lists them.</summary>
    public static IReadOnlyList<string> Actions { get; } =
    [
        AuditActions.Created, AuditActions.Updated, AuditActions.Deleted, AuditActions.SignedIn, AuditActions.SignInFailed,
        AuditActions.Invited, AuditActions.RolesChanged, AuditActions.PasswordResetSent, AuditActions.Deactivated,
        AuditActions.Reactivated, AuditActions.Unlocked, AuditActions.TwoFactorReset, AuditActions.InvoiceSent, AuditActions.ReminderSent,
    ];

    /// <summary>The action as people read it: "Sign-in failed", "Changed roles".</summary>
    public static string Label(string action) => action switch
    {
        AuditActions.SignedIn => "Signed in",
        AuditActions.SignInFailed => "Sign-in failed",
        AuditActions.RolesChanged => "Changed roles",
        AuditActions.PasswordResetSent => "Sent password reset",
        AuditActions.TwoFactorReset => "Reset two-factor",
        AuditActions.InvoiceSent => "Sent invoice",
        AuditActions.ReminderSent => "Sent reminder",
        _ => action,
    };

    /// <summary>Its badge's color: made green, removed red, a failed sign-in amber, a sign-in plain, the rest blue.</summary>
    public static IzzyBlazor.Status Tone(string action) => action switch
    {
        AuditActions.Created => IzzyBlazor.Status.Success,
        AuditActions.Deleted => IzzyBlazor.Status.Danger,
        AuditActions.SignInFailed => IzzyBlazor.Status.Warning,
        AuditActions.SignedIn => IzzyBlazor.Status.Neutral,
        _ => IzzyBlazor.Status.Info,
    };

    /// <summary>What changed, in a few words: "Status, Email", or "6 fields" for more than three.</summary>
    public static string Summary(AuditEvent e)
    {
        var changes = e.ReadChanges();
        return changes.Count switch
        {
            0 => e.Note ?? "",
            <= 3 => string.Join(", ", changes.Select(c => Field(c.Field))),
            _ => $"{changes.Count} fields",
        };
    }

    /// <summary>A field's change as one line: "Status: Active → Paused".</summary>
    public static string Line(AuditChange c) => (c.Before, c.After) switch
    {
        (null, var after) => $"{Field(c.Field)}: {after}",
        (var before, null) => $"{Field(c.Field)}: {before} removed",
        var (before, after) => $"{Field(c.Field)}: {before} → {after}",
    };

    /// <summary>A property's name in words, as the log shows it: LastName is "Last name".</summary>
    public static string Field(string name) =>
        string.Concat(name.Select((ch, i) => i > 0 && char.IsUpper(ch) && !char.IsUpper(name[i - 1]) ? " " + char.ToLowerInvariant(ch) : ch.ToString()));
}
