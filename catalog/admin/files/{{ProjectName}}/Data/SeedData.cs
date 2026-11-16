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
        // A time in working hours on that day, and never later than now (today's come out earlier today).
        DateTime At(DateOnly day)
        {
            var at = day.ToDateTime(new TimeOnly(9 + random.Next(8), random.Next(60)), DateTimeKind.Utc);
            return at < now ? at : now.AddMinutes(-30 - random.Next(120));
        }

        Record(now.AddDays(-2).AddHours(-3), admin, AuditActions.Invited, "User", invitee.Id, invitee.Name,
            [new("Roles", null, Roles.Manager)]);
        Record(now.AddDays(-21), admin, AuditActions.Deactivated, "User", leaver.Id, leaver.Name, note: "Left the company.");
        foreach (var (user, every) in new[] { (admin, 1), (manager, 1), (viewer, 3) })
            for (var day = 13; day >= 0; day -= every)
            {
                var signIn = At(today.AddDays(-day));
                Record(signIn, user, AuditActions.SignedIn, "User", user.Id, user.Name ?? user.Email, note: "With a password.");
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
            // Trials are the newest; the rest joined over two years, more of them lately, as a growing business does.
            var joined = i < 6 ? today.AddDays(-random.Next(2, 20)) : today.AddDays(-(21 + (int)(700 * Math.Pow(random.NextDouble(), 1.6))));
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

        // When the churned ones left: a month or more after they joined, and before today.
        var left = customers.Where(c => c.Status == CustomerStatus.Churned).ToDictionary(c => c.Id, c =>
        {
            var on = c.Joined.AddDays(30 + random.Next(Math.Max(1, today.DayNumber - c.Joined.DayNumber - 31)));
            return on < today ? on : today.AddDays(-1);
        });

        // Some moved up a plan a while after joining.
        var upgraded = customers.Where((c, i) => c.Status != CustomerStatus.Trial && c.Plan > CustomerPlan.Starter && i % 4 == 1)
            .ToDictionary(c => c.Id, c => c.Joined.AddDays(1 + random.Next(Math.Max(1, (left.TryGetValue(c.Id, out var gone) ? gone : today).DayNumber - c.Joined.DayNumber - 1))));

        // A year of invoices, more each month as the business grows; the oldest paid, the newest drafts.
        (string Text, decimal Price)[] items =
        [
            ("Starter plan, monthly", 49m), ("Growth plan, monthly", 249m), ("Business plan, monthly", 599m),
            ("Additional seats (5)", 75m), ("Onboarding session", 350m), ("Priority support, quarterly", 450m), ("Data migration", 1200m),
        ];
        var payers = customers.Where(c => c.Status != CustomerStatus.Trial).ToList();
        // Each paying customer is billed monthly from the day they joined until they left, over the last year.
        var bills = payers.SelectMany(c => Enumerable.Range(0, 24).Select(m => (Customer: c, Issued: c.Joined.AddMonths(m))))
            .Where(b => b.Issued > today.AddDays(-365) && b.Issued <= today
                && !(left.TryGetValue(b.Customer.Id, out var gone) && gone <= b.Issued))
            .OrderBy(b => b.Issued).ToList();
        var invoices = new List<Invoice>();
        foreach (var (customer, issued) in bills)
        {
            var invoice = new Invoice
            {
                Number = $"INV-{invoices.Count + 1:0000}", CustomerId = customer.Id, Customer = customer,
                Issued = issued, Due = issued.AddDays(30), TaxRate = 0.08m,
            };
            var plan = upgraded.TryGetValue(customer.Id, out var up) && issued < up ? customer.Plan - 1 : customer.Plan;
            invoice.Lines.Add(new InvoiceLine { Description = items[(int)plan].Text, UnitPrice = items[(int)plan].Price });
            for (var extra = random.Next(5) == 0 ? 1 + random.Next(2) : 0; extra > 0; extra--)
            {
                var item = items[3 + random.Next(items.Length - 3)];
                invoice.Lines.Add(new InvoiceLine { Description = item.Text, UnitPrice = item.Price, Quantity = item.Text.StartsWith("Additional") ? 1 + random.Next(3) : 1 });
            }
            var age = today.DayNumber - issued.DayNumber;
            var paid = issued.AddDays(random.Next(10) == 0 ? 20 + random.Next(40) : 2 + random.Next(14));
            (InvoiceStatus, DateOnly?, DateOnly?) state =
                age < 7 && random.Next(2) == 0 ? (InvoiceStatus.Draft, null, null)
                : invoices.Count % 97 == 17 ? (InvoiceStatus.Void, issued, null)
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
                new("Status", null, c.Status == CustomerStatus.Trial ? "Trial" : "Active"),
                new("Plan", null, (upgraded.ContainsKey(c.Id) ? c.Plan - 1 : c.Plan).ToString()),
                new("Joined", null, c.Joined.ToString("yyyy-MM-dd")),
            ]);
            if (upgraded.TryGetValue(c.Id, out var up))
                Record(At(up), by, AuditActions.Updated, nameof(Customer), c.Id.ToString(), $"{c.FirstName} {c.LastName}",
                    [new("Plan", (c.Plan - 1).ToString(), c.Plan.ToString())]);
            if (left.TryGetValue(c.Id, out var gone))
                Record(At(gone), by, AuditActions.Updated, nameof(Customer), c.Id.ToString(), $"{c.FirstName} {c.LastName}",
                    [new("Status", "Active", "Churned")]);
        }
        foreach (var i in invoices)
        {
            var by = staff[random.Next(staff.Length)];
            var created = At(i.Issued);
            Record(created, by, AuditActions.Created, nameof(Invoice), i.Id.ToString(), i.Number,
                [new("Customer", null, $"{i.Customer!.FirstName} {i.Customer.LastName}"), new("Total", null, i.Total.ToString("0.00")), new("Due", null, i.Due.ToString("yyyy-MM-dd"))]);
            // Sent the day it was made, a few minutes after.
            if (i.Sent is { } sent) Record(sent == i.Issued ? created.AddMinutes(5 + random.Next(20)) : At(sent), by, AuditActions.InvoiceSent, nameof(Invoice), i.Id.ToString(), i.Number,
                [new("Status", "Draft", "Sent")], note: $"To {i.Customer.Email}.");
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
