using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace {{RootNamespace}}.Data;

/// <summary>
/// Importing contacts (Data/Import.cs): a file needs first_name, last_name and email columns; company and phone
/// are read when they're there. A row matches the contact with its email address, whatever its capitals. A new
/// contact is a lead owned by the import's owner, and a company the file names that isn't here yet is added
/// with it. An existing contact, when the import updates, takes the file's names, and its phone and company
/// where the file has them.
/// </summary>
public static class ContactImport
{
    /// <summary>The columns a file can't do without, as the dialog names them.</summary>
    internal static readonly string[] Needed = ["first_name", "last_name", "email"];

    /// <summary>Other names spreadsheets and exports give the columns, as <see cref="Import.Key"/> makes them.</summary>
    internal static readonly Dictionary<string, string> Aliases = new()
    {
        ["first"] = "firstname", ["givenname"] = "firstname", ["last"] = "lastname", ["surname"] = "lastname", ["familyname"] = "lastname",
        ["emailaddress"] = "email", ["companyname"] = "company", ["organization"] = "company", ["phonenumber"] = "phone", ["mobile"] = "phone",
    };

    internal static async Task<ImportPlan> PlanAsync(Import import, ApplicationDbContext db)
    {
        var contacts = new Dictionary<string, int>();
        foreach (var contact in await db.Contacts.AsNoTracking().Select(c => new { c.Id, c.Email }).ToListAsync()) contacts.TryAdd(contact.Email.ToLowerInvariant(), contact.Id);
        var companies = new Dictionary<string, int>();
        foreach (var company in await db.Companies.AsNoTracking().Select(c => new { c.Id, c.Name }).ToListAsync()) companies.TryAdd(company.Name.ToLowerInvariant(), company.Id);

        var rows = import.Read(Aliases).Rows;
        var seen = new Dictionary<string, int>();
        foreach (var row in rows)
        {
            var email = row["email"]?.ToLowerInvariant() ?? "";
            (row.Name, row.Detail, row.Aside) = ($"{row["firstname"]} {row["lastname"]}".Trim(), row["email"] ?? "No email address", row["company"]);
            row.Problem = Check(row) ?? (seen.TryGetValue(email, out var first) ? $"Same email as row {first}" : null);
            if (row.Problem is not null)
            {
                row.Result = ImportResult.LeftOut;
                continue;
            }

            seen[email] = row.Number;
            if (contacts.TryGetValue(email, out var id)) (row.ExistingId, row.Result) = (id, import.Match == ImportMatch.Update ? ImportResult.Updated : ImportResult.Skipped);
            if (row["company"] is { } named && row.Result != ImportResult.Skipped)
            {
                row.CompanyId = companies.TryGetValue(named.ToLowerInvariant(), out var known) ? known : null;
                row.NewCompany = row.CompanyId is null;
                row.AsideNote = row.NewCompany ? "New company" : null;
            }
        }
        return new ImportPlan(rows);
    }

    internal static async Task ApplyAsync(Import import, ImportPlan plan, ApplicationDbContext db, DateOnly today)
    {
        // The companies first, one of each however many rows name it, so the contacts are saved (and logged)
        // with their companies' ids.
        var added = new Dictionary<string, Company>();
        foreach (var name in plan.Incoming.Where(r => r.NewCompany).Select(r => r["company"]!))
        {
            added.TryAdd(name.ToLowerInvariant(), new Company { Name = name, OwnerId = import.OwnerId, Status = CompanyStatus.Prospect, Added = today });
        }
        db.Companies.AddRange(added.Values);
        await db.SaveChangesAsync();

        var ids = plan.Incoming.Where(r => r.ExistingId is not null).Select(r => r.ExistingId!.Value).ToList();
        var existing = await db.Contacts.Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        foreach (var row in plan.Incoming)
        {
            var contact = row.ExistingId is { } id ? existing.GetValueOrDefault(id)
                : new Contact { Email = row["email"]!, OwnerId = import.OwnerId, Status = ContactStatus.Lead, Added = today };
            if (contact is null) continue;
            (contact.FirstName, contact.LastName, contact.Phone) = (row["firstname"]!, row["lastname"]!, row["phone"] ?? contact.Phone);
            if (row["company"] is { } company) contact.CompanyId = row.CompanyId ?? added[company.ToLowerInvariant()].Id;
            if (row.ExistingId is null) db.Contacts.Add(contact);
        }
    }

    // Why a row can't be a contact, if it can't: the contact form's rules (Data/Contact.cs).
    private static string? Check(ImportRow row) =>
        row["firstname"] is null ? "No first name"
        : row["lastname"] is null ? "No last name"
        : row["email"] is not { } email ? "No email address"
        : !new EmailAddressAttribute().IsValid(email) ? "Not an email address"
        : row["phone"] is { } phone && !new PhoneAttribute().IsValid(phone) ? "Not a phone number"
        : row["firstname"]!.Length > 60 || row["lastname"]!.Length > 60 ? "Name is too long"
        : email.Length > 256 ? "Email address is too long"
        : row["phone"]?.Length > 40 ? "Phone number is too long"
        : row["company"]?.Length > 120 ? "Company name is too long"
        : null;
}
