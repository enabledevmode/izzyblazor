using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using {{RootNamespace}}.Data;

namespace {{RootNamespace}}.Email;

/// <summary>
/// Emails an activity's reminder to whoever it's assigned to, as long before it starts as the activity asks
/// (Activity.Reminder). It looks once a minute while the app runs, and only when mail is set up
/// (<see cref="Mailer.Sends"/>). Each reminder goes once, whichever copy of the app sees it first: the activity is
/// marked (Activity.Reminded) before the mail is sent, and unmarked if the mail can't be sent, to try again. One whose time has passed is not sent late, and someone
/// deactivated gets none.
/// </summary>
public sealed class Reminders(IServiceScopeFactory scopes, Mailer mailer, IOptions<EmailOptions> options, ILogger<Reminders> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        if (!mailer.Sends) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await SendDueAsync(stopping);
            }
            catch (Exception problem) when (problem is not OperationCanceledException)
            {
                log.LogError(problem, "Couldn't send the reminders that are due.");
            }
        }
        while (await timer.WaitForNextTickAsync(stopping));
    }

    private async Task SendDueAsync(CancellationToken stopping)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var workspace = await db.Workspaces.AsNoTracking().FirstOrDefaultAsync(stopping);

        // Activities keep the workspace's own time. Those starting within the longest reminder, then each by its own.
        var now = Clock.Now(workspace);
        var latest = now.AddMinutes(Activity.Reminders.Max());
        var ahead = await db.Activities.AsNoTracking().Include(a => a.AssignedTo).Include(a => a.Contact!.Company)
            .Where(a => a.Reminder != null && a.Reminded == null && a.Completed == null && a.AssignedToId != null && a.Start > now && a.Start <= latest)
            .ToListAsync(stopping);

        foreach (var activity in ahead.Where(a => a.Start.AddMinutes(-a.Reminder!.Value) <= now))
        {
            // Marked first, and only by one: another copy of the app finds it taken.
            var mine = await db.Activities.Where(a => a.Id == activity.Id && a.Reminded == null)
                .ExecuteUpdateAsync(set => set.SetProperty(a => a.Reminded, DateTime.UtcNow), stopping);
            // Not to someone deactivated (Identity's lockout: Admin/UserAdmin.cs).
            if (mine == 0 || activity.AssignedTo is not { Email: { } to } assignee || assignee.LockoutEnd > DateTimeOffset.UtcNow) continue;

            var today = DateOnly.FromDateTime(now);
            var when = $"{When.Day(DateOnly.FromDateTime(activity.Start), today)} at {When.Time(activity.Start)}";
            var with = activity.Contact is { } contact ? $" with {contact.Name}{(contact.Company is { } company ? $" ({company.Name})" : "")}" : "";
            List<string> lines = [activity.Type == ActivityType.Task ? $"Due {when}{with}." : $"{activity.Type} {when}{with}."];
            if (activity.Location is { Length: > 0 } where) lines.Add(where);

            // The link needs the app's own address, which only configuration knows here (Email:AppUrl).
            var link = options.Value.AppUrl is { Length: > 0 } app ? $"{app.TrimEnd('/')}/activities/{activity.Id}" : null;
            if (!await mailer.SendAsync(to, $"Reminder: {activity.Title}", workspace?.Name ?? "Reminders", lines, "Open the activity", link))
                await db.Activities.Where(a => a.Id == activity.Id).ExecuteUpdateAsync(set => set.SetProperty(a => a.Reminded, (DateTime?)null), stopping);
        }
    }
}
