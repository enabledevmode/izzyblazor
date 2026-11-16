using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using {{RootNamespace}}.Security;

namespace {{RootNamespace}}.Data;

/// <summary>
/// Makes the database on first run (applying the migrations) and, while it has no users, fills it: the roles, an
/// account for each, 48 customers, a year of invoices up to today, and the audit history behind them, so every
/// page has something to show. Delete Data/app.db to start again; remove the call in Program.cs to start empty.
/// </summary>
public static class SeedData
{
    public static async Task SeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        if (await db.Users.AnyAsync()) return;

        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SeedData));
        var config = app.Configuration.GetSection("Seed");
        var domain = config["Domain"] ?? "example.com";
        var password = config["Password"];
        if (string.IsNullOrEmpty(password))
        {
            password = RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789", 16) + "-1a";
            log.LogWarning("Made the sample accounts. Sign in as admin@{Domain} with {Password}, then change it: this is the only time it's shown.", domain, password);
        }

        db.Auditing = false;
        var random = new Random(2026);
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var events = new List<AuditEvent>();

        // Roles and people.
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All) await roles.CreateAsync(new IdentityRole(role));
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        async Task<ApplicationUser> Person(string name, string email, string role, int daysAgo, bool invited = false, bool deactivated = false)
        {
            var user = new ApplicationUser
            {
                UserName = $"{email}@{domain}", Email = $"{email}@{domain}", Name = name,
                EmailConfirmed = !invited, Created = now.AddDays(-daysAgo),
            };
            var made = invited ? await users.CreateAsync(user) : await users.CreateAsync(user, password);
            if (!made.Succeeded) throw new InvalidOperationException($"Couldn't make {user.Email}: {string.Join(" ", made.Errors.Select(e => e.Description))}");
            await users.AddToRoleAsync(user, role);
            if (deactivated) await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            return user;
        }
        var admin = await Person("Jordan Lee", "admin", Roles.Admin, 400);
        var manager = await Person("Sam Rivera", "manager", Roles.Manager, 380);
        var viewer = await Person("Alex Kim", "viewer", Roles.Viewer, 200);
        var invitee = await Person("Taylor Brooks", "taylor.brooks", Roles.Manager, 2, invited: true);
        var leaver = await Person("Chris Park", "chris.park", Roles.Viewer, 300, deactivated: true);
        ApplicationUser[] staff = [admin, manager, manager];

        void Record(DateTime time, ApplicationUser? by, string action, string? type, string? id, string? name, IReadOnlyList<AuditChange>? changes = null, string? note = null)
        {
            if (time > now) return;
            events.Add(new AuditEvent
            {
                Time = time, ActorId = by?.Id, ActorName = by?.Name, Action = action, EntityType = type, EntityId = id,
                EntityName = name, Changes = changes is null ? null : AuditEvent.WriteChanges(changes), Note = note,
                IpAddress = by is null ? null : $"203.0.113.{Array.IndexOf<ApplicationUser>([admin, manager, viewer, leaver], by) + 10}",
            });
        }
        DateTime At(DateOnly day) => day.ToDateTime(new TimeOnly(9 + random.Next(8), random.Next(60)), DateTimeKind.Utc);

        Record(now.AddDays(-2).AddHours(-3), admin, AuditActions.Invited, "User", invitee.Id, invitee.Email,
            [new("Roles", null, Roles.Manager)]);
        Record(now.AddDays(-21), admin, AuditActions.Deactivated, "User", leaver.Id, leaver.Email, note: "Left the company.");
        foreach (var (user, every) in new[] { (admin, 1), (manager, 1), (viewer, 3) })
            for (var day = 13; day >= 0; day -= every)
            {
                var signIn = At(today.AddDays(-day));
                Record(signIn, user, AuditActions.SignedIn, "User", user.Id, user.Email, note: "With a password.");
                user.LastSignIn = signIn > now ? user.LastSignIn : signIn;
            }
        Record(now.AddDays(-5), null, AuditActions.SignInFailed, null, null, null, note: $"No account for jordan@{domain}.");
        foreach (var user in new[] { admin, manager, viewer }) await users.UpdateAsync(user);

        // Customers: most active, some on a trial (the newest), some gone.
        string[] first = ["Amara", "Lucas", "Priya", "Jonas", "Sofia", "Mateo", "Hannah", "Daniel", "Chloe", "Kwame", "Elena", "Noah",
            "Aiko", "Liam", "Isabel", "Omar", "Freya", "Ravi", "Maya", "Tomás", "Leila", "Ethan", "Nadia", "Felix"];
        string[] last = ["Okafor", "Moreau", "Raman", "Becker", "Lindqvist", "García", "Kim", "Novak", "Dubois", "Mensah", "Rossi", "Fischer",
            "Tanaka", "O'Brien", "Santos", "Haddad", "Jensen", "Patel", "Chen", "Silva", "Nasser", "Walsh", "Petrova", "Wagner"];
        var customers = new List<Customer>();
        for (var i = 0; i < 48; i++)
        {
            var (f, l) = (first[i % first.Length], last[(i * 7 + i / first.Length) % last.Length]);
            var joined = i < 6 ? today.AddDays(-random.Next(2, 20)) : today.AddDays(-random.Next(30, 1000));
            customers.Add(new Customer
            {
                FirstName = f, LastName = l,
                Email = $"{Plain(f)}.{Plain(l)}@{domain}",
                Status = i < 6 ? CustomerStatus.Trial : i % 8 == 3 ? CustomerStatus.Churned : CustomerStatus.Active,
                Plan = (CustomerPlan)(random.Next(10) switch { < 4 => 0, < 8 => 1, _ => 2 }),
                Joined = joined,
            });
        }
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();

        // A year of invoices, more each month as the business grows; the oldest paid, the newest drafts.
        (string Text, decimal Price)[] items =
        [
            ("Starter plan, monthly", 49m), ("Growth plan, monthly", 249m), ("Business plan, monthly", 599m),
            ("Additional seats (5)", 75m), ("Onboarding session", 350m), ("Priority support, quarterly", 450m), ("Data migration", 1200m),
        ];
        var payers = customers.Where(c => c.Status != CustomerStatus.Trial).ToList();
        var days = Enumerable.Range(0, 120).Select(_ => (int)(365 * Math.Sqrt(random.NextDouble()))).Select(d => today.AddDays(-365 + d)).Order().ToList();
        var invoices = new List<Invoice>();
        foreach (var issued in days)
        {
            var customer = payers.Where(c => c.Joined <= issued).OrderBy(_ => random.Next()).FirstOrDefault() ?? payers[0];
            var invoice = new Invoice
            {
                Number = $"INV-{invoices.Count + 1:0000}", CustomerId = customer.Id, Customer = customer,
                Issued = issued, Due = issued.AddDays(30), TaxRate = 0.08m,
            };
            invoice.Lines.Add(new InvoiceLine { Description = items[(int)customer.Plan].Text, UnitPrice = items[(int)customer.Plan].Price });
            for (var extra = random.Next(3); extra > 0; extra--)
            {
                var item = items[3 + random.Next(items.Length - 3)];
                invoice.Lines.Add(new InvoiceLine { Description = item.Text, UnitPrice = item.Price, Quantity = item.Text.StartsWith("Additional") ? 1 + random.Next(3) : 1 });
            }
            var age = today.DayNumber - issued.DayNumber;
            var paid = issued.AddDays(3 + random.Next(age > 45 ? 35 : 60));
            (InvoiceStatus, DateOnly?, DateOnly?) state =
                age < 7 && random.Next(2) == 0 ? (InvoiceStatus.Draft, null, null)
                : invoices.Count is 17 or 64 ? (InvoiceStatus.Void, issued, null)
                : paid <= today ? (InvoiceStatus.Paid, issued, paid)
                : (InvoiceStatus.Sent, issued, null);
            (invoice.Status, invoice.Sent, invoice.Paid) = state;
            if (invoice.IsOverdue(today) && invoice.Due.AddDays(7) <= today) invoice.LastReminder = invoice.Due.AddDays(7);
            if (invoice.Status == InvoiceStatus.Paid) customer.LifetimeValue += invoice.Total;
            invoices.Add(invoice);
        }
        db.Invoices.AddRange(invoices);
        await db.SaveChangesAsync();

        // The history behind them, as the audit log would have written it.
        foreach (var c in customers)
        {
            var by = staff[random.Next(staff.Length)];
            Record(At(c.Joined), by, AuditActions.Created, nameof(Customer), c.Id.ToString(), $"{c.FirstName} {c.LastName}",
            [
                new("FirstName", null, c.FirstName), new("LastName", null, c.LastName), new("Email", null, c.Email),
                new("Status", null, c.Status == CustomerStatus.Trial ? "Trial" : "Active"), new("Plan", null, c.Plan.ToString()),
                new("Joined", null, c.Joined.ToString("yyyy-MM-dd")),
            ]);
            if (c.Status == CustomerStatus.Churned)
                Record(At(c.Joined.AddDays(60 + random.Next(200))), by, AuditActions.Updated, nameof(Customer), c.Id.ToString(), $"{c.FirstName} {c.LastName}",
                    [new("Status", "Active", "Churned")]);
        }
        foreach (var i in invoices)
        {
            var by = staff[random.Next(staff.Length)];
            Record(At(i.Issued), by, AuditActions.Created, nameof(Invoice), i.Id.ToString(), i.Number,
                [new("Customer", null, $"{i.Customer!.FirstName} {i.Customer.LastName}"), new("Total", null, i.Total.ToString("0.00")), new("Due", null, i.Due.ToString("yyyy-MM-dd"))]);
            if (i.Sent is { } sent) Record(At(sent).AddMinutes(20), by, AuditActions.InvoiceSent, nameof(Invoice), i.Id.ToString(), i.Number, note: $"To {i.Customer.Email}.");
            if (i.LastReminder is { } reminded) Record(At(reminded), by, AuditActions.ReminderSent, nameof(Invoice), i.Id.ToString(), i.Number, note: $"To {i.Customer.Email}.");
            if (i.Status == InvoiceStatus.Void) Record(At(i.Issued.AddDays(2)), admin, AuditActions.Updated, nameof(Invoice), i.Id.ToString(), i.Number, [new("Status", "Sent", "Void")]);
            if (i.Paid is { } paid) Record(At(paid), by, AuditActions.Updated, nameof(Invoice), i.Id.ToString(), i.Number,
                [new("Status", "Sent", "Paid"), new("Paid", null, paid.ToString("yyyy-MM-dd"))]);
        }
        db.AuditEvents.AddRange(events.OrderBy(e => e.Time));
        await db.SaveChangesAsync();
        db.Auditing = true;
    }

    // A name as an email's local part: Tomás O'Brien → tomas.obrien.
    static string Plain(string name) =>
        new string(name.Normalize(System.Text.NormalizationForm.FormD).Where(char.IsAsciiLetter).ToArray()).ToLowerInvariant();
}
