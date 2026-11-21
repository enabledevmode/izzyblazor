using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using {{RootNamespace}}.Security;

namespace {{RootNamespace}}.Data;

/// <summary>
/// The sample data a new database starts with when Seed:SampleData is on (appsettings.Development.json has it on;
/// Data/Setup.cs asks): a team member for each role, four companies and their people, the deals in the pipeline
/// and those won and lost over the last three months, this week's activities, a note, and the audit history
/// behind them, so every page has something to show. Dates are set around today. Delete Data/app.db to start
/// again; turn Seed:SampleData off to start with one admin and nothing else.
/// </summary>
public static class SampleData
{
    /// <summary>Fills an empty database, naming its <paramref name="workspace"/> for the sample team.</summary>
    public static async Task AddAsync(IServiceProvider services, IConfiguration configuration, ApplicationDbContext db, Workspace workspace)
    {
        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SampleData));
        var config = configuration.GetSection("Seed");
        var domain = config["Domain"] ?? "northstar.example";
        var password = config["Password"];
        if (string.IsNullOrEmpty(password))
        {
            password = Setup.NewPassword();
            log.LogWarning("Made the sample accounts. Log in as jamie@{Domain} (Admin) or alex@{Domain} (Manager) with {Password}, then change both: this is the only time it's shown.", domain, domain, password);
        }

        db.Auditing = false;
        (workspace.Name, workspace.Website) = ("Northstar Studio", $"https://{domain}");

        // The workspace's clock: activities are scheduled in its time zone, the audit log is in UTC.
        var zone = TimeZoneInfo.FindSystemTimeZoneById(workspace.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
        DateOnly Day(int days) => today.AddDays(days);
        DateTime Local(int days, int hour, int minute = 0) => Day(days).ToDateTime(new TimeOnly(hour, minute));
        DateTime Utc(int days, int hour, int minute = 0) => TimeZoneInfo.ConvertTimeToUtc(Local(days, hour, minute), zone);

        // The team.
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        async Task<ApplicationUser> Person(string name, string email, string role, DateTime created, bool invited = false)
        {
            var user = new ApplicationUser
            {
                UserName = $"{email}@{domain}", Email = $"{email}@{domain}", Name = name,
                EmailConfirmed = !invited, Created = created,
            };
            var made = invited ? await users.CreateAsync(user) : await users.CreateAsync(user, password);
            if (!made.Succeeded) throw new InvalidOperationException($"Couldn't make {user.Email}: {string.Join(" ", made.Errors.Select(e => e.Description))}");
            await users.AddToRoleAsync(user, role);
            return user;
        }
        var jamie = await Person("Jamie Davis", "jamie", Roles.Admin, Utc(-90, 9));
        var alex = await Person("Alex Morgan", "alex", Roles.Manager, Utc(-80, 10));
        var taylor = await Person("Taylor Kim", "taylor", Roles.Viewer, Utc(-3, 10), invited: true);
        // What each seller aims to win in a month: Reports sets the team against it.
        (jamie.MonthlyTarget, alex.MonthlyTarget) = (20_000, 20_000);

        // Companies and their people.
        Company Account(string name, string site, ApplicationUser owner, CompanyStatus status, int added) =>
            new() { Name = name, Website = site, OwnerId = owner.Id, Status = status, Added = Day(added), Country = "United States" };
        var acme = Account("Acme Co.", "acme.example", jamie, CompanyStatus.Customer, -7);
        (acme.Industry, acme.Size, acme.City) = (Industry.DesignTechnology, CompanySize.UpTo50, "Brooklyn, New York");
        acme.Notes = "Expanding the marketing team. Website launch planned for January. Olivia coordinates proposals; Ethan signs off on operations.";
        var brightside = Account("Brightside", "brightside.example", jamie, CompanyStatus.Prospect, -20);
        var fieldwork = Account("Fieldwork", "fieldwork.example", alex, CompanyStatus.Prospect, -6);
        var evergreen = Account("Evergreen", "evergreen.example", jamie, CompanyStatus.Prospect, -9);
        db.Companies.AddRange(acme, brightside, fieldwork, evergreen);

        var phones = 0;
        Contact Someone(string first, string last, Company company, ContactStatus status, ApplicationUser owner, DateTime last_, int added, string? title = null) =>
            new()
            {
                FirstName = first, LastName = last, Email = $"{first.ToLowerInvariant()}@{company.Website}", Company = company,
                JobTitle = title, Status = status, OwnerId = owner.Id, Source = LeadSource.Referral, LastActivity = last_, Added = Day(added),
                // Numbers kept for fiction (555-01xx), one each.
                Phone = $"(212) 555-01{++phones:00}",
            };
        var olivia = Someone("Olivia", "Chen", acme, ContactStatus.Customer, jamie, Utc(0, 9, 15), -7, "Marketing director");
        olivia.NextFollowUp = Day(2);
        olivia.Phone = "(212) 555-0148";
        olivia.Notes = "Prefers email for proposals and a short call for scope changes.\n\nBudget agreed in principle. Confirm the final decision-maker before closing.";
        var marcus = Someone("Marcus", "Reed", brightside, ContactStatus.Lead, jamie, Utc(-1, 15), -20);
        var sofia = Someone("Sofia", "Patel", fieldwork, ContactStatus.Prospect, alex, Utc(-3, 14), -1);
        var ethan = Someone("Ethan", "Brooks", acme, ContactStatus.Customer, alex, Utc(-4, 11), -7, "Operations lead");
        var amara = Someone("Amara", "Okafor", evergreen, ContactStatus.Prospect, jamie, Utc(-5, 13), -9);
        var leo = Someone("Leo", "Martinez", brightside, ContactStatus.Lead, alex, Utc(-7, 10), -20);
        db.Contacts.AddRange(olivia, marcus, sofia, ethan, amara, leo);

        // The pipeline, and what was won this month and the two before it.
        Deal Opportunity(string name, Company company, Contact contact, decimal value, DealStage stage, int closes, int created = -7) =>
            new()
            {
                Name = name, Company = company, Contact = contact, OwnerId = company.OwnerId, Value = value, Stage = stage,
                ExpectedClose = Day(closes), Source = DealSource.Referral, Created = Day(created),
            };
        var website = Opportunity("Website redesign", acme, olivia, 18_000, DealStage.Proposal, 10);
        website.Scope = "Discovery, experience design, responsive implementation, and launch support. Content migration is included; ongoing maintenance is scoped separately.";
        var identity = Opportunity("Brand identity", brightside, marcus, 8_500, DealStage.Qualified, 17);
        var portal = Opportunity("Customer portal", fieldwork, sofia, 24_000, DealStage.Discovery, 24, -5);
        var retainer = Opportunity("Quarterly retainer", evergreen, amara, 12_000, DealStage.Negotiation, 3, -9);
        // Two for Reports' Deals at risk: nothing has happened on the landing pages since they were proposed,
        // and Brand identity (below) has no next step.
        var landing = Opportunity("Campaign landing pages", brightside, leo, 6_000, DealStage.Proposal, 14, -16);
        var photography = Opportunity("Product photography", acme, olivia, 4_500, DealStage.Qualified, 21, -6);
        (identity.Source, portal.Source, landing.Source, identity.OwnerId) = (DealSource.Website, DealSource.Outbound, DealSource.Website, alex.Id);
        db.Deals.AddRange(website, identity, portal, retainer, landing, photography);

        // Won on days in this month and the last two, whatever today is.
        var month = new DateOnly(today.Year, today.Month, 1);
        Deal Won(string name, Company company, Contact contact, decimal value, int monthsAgo, int day, DealSource source = DealSource.Referral, ApplicationUser? owner = null, DealStage stage = DealStage.Won)
        {
            var closed = month.AddMonths(-monthsAgo).AddDays(day - 1);
            if (closed > today) closed = today;
            var deal = Opportunity(name, company, contact, value, stage, 0, 0);
            (deal.ExpectedClose, deal.Closed, deal.Created, deal.Source) = (closed, closed, closed.AddDays(-30), source);
            if (owner is not null) deal.OwnerId = owner.Id;
            return deal;
        }
        Deal Lost(string name, Company company, Contact contact, decimal value, int monthsAgo, int day, DealSource source, ApplicationUser owner) =>
            Won(name, company, contact, value, monthsAgo, day, source, owner, DealStage.Lost);
        db.Deals.AddRange(
            Won("Brand guidelines", brightside, marcus, 12_000, 0, 3), Won("Launch campaign", acme, ethan, 9_000, 0, 6, DealSource.Website, alex),
            Won("Website audit", fieldwork, sofia, 5_500, 0, 9, DealSource.Outbound, jamie), Won("Annual report design", evergreen, amara, 14_000, 1, 8),
            Won("Onboarding emails", acme, olivia, 8_500, 1, 21, DealSource.ExistingCustomer, alex), Won("Trade show booth", brightside, leo, 11_000, 2, 11, DealSource.Outbound, alex),
            Won("Product one-pagers", evergreen, amara, 8_000, 2, 24, DealSource.Website),
            Lost("Packaging refresh", fieldwork, sofia, 7_000, 0, 4, DealSource.Outbound, alex), Lost("Event signage", evergreen, amara, 3_500, 0, 8, DealSource.Website, jamie),
            Lost("Social media kit", brightside, leo, 5_000, 1, 15, DealSource.Outbound, alex));

        // This week: what's due, what's on the calendar, and what's been done.
        Activity Do(ActivityType type, string title, Contact? contact, Deal? deal, DateTime start, int minutes, ApplicationUser who, DateTime? done = null, string? agenda = null) =>
            new()
            {
                Type = type, Title = title, Contact = contact, Deal = deal, Start = start, Duration = minutes,
                AssignedToId = who.Id, CreatedById = who.Id, Completed = done, Agenda = agenda,
            };
        var followUp = Do(ActivityType.Meeting, "Proposal follow-up", olivia, website, Local(2, 10), 30, jamie, agenda:
            """
            Walk through the revised proposal and agree on a decision date.

            - Confirm the final scope and content responsibilities.
            - Review pricing and payment milestones.
            - Identify the remaining approval steps.
            """);
        followUp.Location = "Video call · Link to be added";
        var walkthrough = Do(ActivityType.Meeting, "Acme website walkthrough", olivia, website, Local(0, 14), 45, jamie);
        walkthrough.Location = "https://meet.example/acme-walkthrough";
        var discovery = Do(ActivityType.Call, "Discovery call", sofia, portal, Local(-1, 9, 30), 30, alex, Utc(-1, 10));
        db.Activities.AddRange(
            Do(ActivityType.Task, "Follow up on website proposal", olivia, website, Local(-1, 9), 30, jamie),
            Do(ActivityType.Task, "Send discovery questions", sofia, null, Local(0, 11), 30, jamie),
            Do(ActivityType.Task, "Confirm retainer scope", amara, null, Local(0, 15), 30, jamie),
            Do(ActivityType.Task, "Send brand questionnaire", marcus, null, Local(0, 16), 30, jamie),
            Do(ActivityType.Task, "Share landing page estimate", leo, landing, Local(0, 16, 30), 30, jamie),
            Do(ActivityType.Task, "Book product shoot dates", ethan, photography, Local(0, 17), 30, jamie),
            walkthrough,
            followUp,
            Do(ActivityType.Task, "Review proposal with Olivia", null, website, Local(2, 9), 30, jamie),
            Do(ActivityType.Task, "Prepare discovery agenda", null, portal, Local(3, 9), 30, jamie),
            Do(ActivityType.Meeting, "Scope review", amara, retainer, Local(3, 13), 30, jamie),
            discovery,
            Do(ActivityType.Task, "Proposal shared", olivia, website, Local(0, 9, 15), 15, jamie, Utc(0, 9, 15),
                "Shared the revised scope and pricing. Follow up after the team’s internal review."),
            Do(ActivityType.Call, "Discovery call", olivia, website, Local(-3, 11), 25, jamie, Utc(-3, 11, 25),
                "Discussed goals, timeline, and decision process. The team is aiming for a January launch."));

        db.Notes.Add(new Note
        {
            Title = "Discovery call notes", Contact = olivia, Deal = website, AuthorId = jamie.Id, Created = Utc(-3, 12, 10), Edited = Utc(-3, 12, 10),
            Body =
                """
                Olivia wants a clearer story for the website and a more straightforward path from product pages to a sales conversation.

                ### Priorities

                - Launch the new website in January.
                - Make the case studies easier to find and maintain.
                - Keep the existing analytics setup.

                ### Budget and approvals

                The proposed $18,000 budget is within range. Ethan will review operations requirements before the final approval.

                ### Next steps

                Send a revised proposal with content responsibilities and a phased launch timeline. Follow up on November 19.
                """,
        });
        await db.SaveChangesAsync();

        // The history behind them, as the audit log would have written it.
        var events = new List<AuditEvent>();
        void Record(DateTime time, ApplicationUser by, string action, string type, object id, string name, params AuditChange[] changes) =>
            events.Add(new AuditEvent
            {
                Time = time, ActorId = by.Id, ActorName = by.Name, Action = action, EntityType = type, EntityId = id.ToString(),
                EntityName = name, Changes = AuditEvent.WriteChanges(changes),
            });
        foreach (var company in new[] { acme, brightside, fieldwork, evergreen })
            Record(company.Added.ToDateTime(new TimeOnly(14, 0), DateTimeKind.Utc), company.OwnerId == alex.Id ? alex : jamie,
                AuditActions.Created, nameof(Company), company.Id, company.Name, new AuditChange("Name", null, company.Name));
        foreach (var contact in new[] { olivia, marcus, ethan, amara, leo })
            Record(contact.Added.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc), contact.OwnerId == alex.Id ? alex : jamie,
                AuditActions.Created, nameof(Contact), contact.Id, contact.Name, new AuditChange("Company", null, contact.Company!.Name));
        // When the meetings ahead were put on the calendar: a company's Activity tab says who scheduled each, and when.
        Record(Utc(-1, 16, 20), jamie, AuditActions.Created, nameof(Activity), walkthrough.Id, walkthrough.Title, new AuditChange("Title", null, walkthrough.Title));
        Record(Utc(-3, 11, 40), jamie, AuditActions.Created, nameof(Activity), followUp.Id, followUp.Title, new AuditChange("Title", null, followUp.Title));
        Record(Utc(-3, 10), jamie, AuditActions.Invited, "User", taylor.Id, taylor.Name!, new AuditChange("Role", null, Roles.Viewer));
        Record(Utc(-4, 11, 45), alex, AuditActions.Completed, nameof(Activity), discovery.Id, discovery.Title, new AuditChange("Status", null, "Completed"));
        Record(Utc(-3, 16, 15), jamie, AuditActions.StageChanged, nameof(Deal), retainer.Id, retainer.Name, new AuditChange("Stage", "Proposal", "Negotiation"));
        Record(Utc(-1, 14, 30), alex, AuditActions.Created, nameof(Contact), sofia.Id, sofia.Name, new AuditChange("Company", null, fieldwork.Name));
        Record(Utc(0, 9, 15), jamie, AuditActions.Updated, nameof(Deal), website.Id, website.Name, new AuditChange("Value", "16000", "18000"));
        // Nothing in the log is later than now, whatever time of day the app first runs.
        var now = DateTime.UtcNow;
        db.AuditEvents.AddRange(events.Where(e => e.Time <= now).OrderBy(e => e.Time));
        await db.SaveChangesAsync();
        db.Auditing = true;
    }
}
