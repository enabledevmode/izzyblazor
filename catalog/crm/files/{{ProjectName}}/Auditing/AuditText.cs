using {{RootNamespace}}.Data;

namespace {{RootNamespace}}.Auditing;

/// <summary>How the audit log words and colors each entry (Components/Pages/Settings/AuditLog.razor).</summary>
public static class AuditText
{
    /// <summary>Every action, in the order the Action filter lists them.</summary>
    public static IReadOnlyList<string> Actions { get; } =
    [
        AuditActions.Created, AuditActions.Updated, AuditActions.Deleted, AuditActions.SignedIn, AuditActions.SignInFailed,
        AuditActions.Invited, AuditActions.RolesChanged, AuditActions.PasswordResetSent, AuditActions.Deactivated,
        AuditActions.Reactivated, AuditActions.Unlocked, AuditActions.TwoFactorReset, AuditActions.StageChanged, AuditActions.Completed,
    ];

    /// <summary>The action as people read it: "Login failed", "Changed roles".</summary>
    public static string Label(string action) => action switch
    {
        AuditActions.SignedIn => "Signed in",
        AuditActions.SignInFailed => "Login failed",
        AuditActions.RolesChanged => "Changed roles",
        AuditActions.PasswordResetSent => "Sent password reset",
        AuditActions.TwoFactorReset => "Reset two-factor",
        AuditActions.StageChanged => "Changed stage",
        _ => action,
    };

    /// <summary>What was done, and to what: "Updated deal", "Created contact", "Invited member".</summary>
    public static string Label(AuditEvent e) => e.Action switch
    {
        AuditActions.Created or AuditActions.Updated or AuditActions.Deleted or AuditActions.Completed when e.EntityType is { } type =>
            $"{e.Action} {(type == "User" ? "member" : Field(type).ToLowerInvariant())}",
        AuditActions.Invited => "Invited member",
        AuditActions.Deactivated => "Deactivated member",
        AuditActions.Reactivated => "Reactivated member",
        AuditActions.Unlocked => "Unlocked member",
        _ => Label(e.Action),
    };

    /// <summary>Its badge's color: made or finished green, removed red, a failed login amber, an invite or a login plain, the rest blue.</summary>
    public static IzzyBlazor.Status Tone(string action) => action switch
    {
        AuditActions.Created or AuditActions.Completed => IzzyBlazor.Status.Success,
        AuditActions.Deleted => IzzyBlazor.Status.Danger,
        AuditActions.SignInFailed => IzzyBlazor.Status.Warning,
        AuditActions.SignedIn or AuditActions.Invited => IzzyBlazor.Status.Neutral,
        _ => IzzyBlazor.Status.Info,
    };

    /// <summary>
    /// What changed, in a line: "Value: $16,000 → $18,000", "Proposal → Negotiation", "Added to Fieldwork". A
    /// second change is counted ("+2 more"); a new record's own fields aren't listed.
    /// </summary>
    public static string Change(AuditEvent e, Workspace? workspace)
    {
        var changes = e.ReadChanges();
        if (changes.Count == 0) return e.Note ?? "";
        if (e.Action == AuditActions.Created)
            return changes.FirstOrDefault(c => c.Field == "Company")?.After is { } company ? $"Added to {company}" : "";
        if (e.Action == AuditActions.Deleted) return "";
        var first = changes[0];
        var line = e.Action == AuditActions.StageChanged ? $"{first.Before} → {first.After}" : Line(first with { Before = Value(first.Field, first.Before, workspace), After = Value(first.Field, first.After, workspace) });
        return changes.Count == 1 ? line : $"{line} +{changes.Count - 1} more";
    }

    // An amount as the app writes amounts; anything else as the log kept it.
    static string? Value(string field, string? value, Workspace? workspace) =>
        workspace is not null && field is "Value" or "MonthlyTarget" && decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount)
            ? workspace!.Money(amount) : value;

    /// <summary>A field's change as one line: "Status: Lead → Customer".</summary>
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
