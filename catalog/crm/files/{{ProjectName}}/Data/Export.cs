using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using {{RootNamespace}}.Security;

namespace {{RootNamespace}}.Data;

/// <summary>
/// Contacts, companies and deals as CSV files, for a spreadsheet or another tool: every record, by name. The
/// contact and company files have the columns Import reads (Data/ContactImport.cs, Data/CompanyImport.cs), so one
/// can be changed and brought back (a contact needs an email address for that: Import matches by it). Anyone who may see records may download them; the Export buttons are on the
/// three lists.
/// </summary>
public static class Export
{
    /// <summary>Maps /contacts/export, /companies/export and /deals/export.</summary>
    public static void MapExports(this WebApplication app)
    {
        app.MapGet("/contacts/export", async (ApplicationDbContext db) =>
        {
            var contacts = await db.Contacts.AsNoTracking().Include(c => c.Company).Include(c => c.Owner).OrderBy(c => c.FirstName).ThenBy(c => c.LastName).ToListAsync();
            return File("contacts", ["first_name", "last_name", "email", "company", "phone", "job_title", "status", "lead_source", "owner", "added"],
                contacts.Select(c => new[]
                {
                    c.FirstName, c.LastName, c.Email, c.Company?.Name, c.Phone, c.JobTitle, c.Status.ToString(),
                    c.Source is { } source ? Names.Of(source) : null, c.Owner?.Name, Day(c.Added),
                }));
        }).RequireAuthorization(Policies.ViewRecords);

        app.MapGet("/companies/export", async (ApplicationDbContext db) =>
        {
            var companies = await db.Companies.AsNoTracking().Include(c => c.Owner).OrderBy(c => c.Name).ToListAsync();
            return File("companies", ["name", "website", "industry", "size", "city", "country", "status", "owner", "added"],
                companies.Select(c => new[]
                {
                    c.Name, c.Website, c.Industry is { } industry ? Names.Of(industry) : null, c.Size is { } size ? Names.Of(size) : null,
                    c.City, c.Country, c.Status.ToString(), c.Owner?.Name, Day(c.Added),
                }));
        }).RequireAuthorization(Policies.ViewRecords);

        app.MapGet("/deals/export", async (ApplicationDbContext db) =>
        {
            var deals = await db.Deals.AsNoTracking().Include(d => d.Company).Include(d => d.Contact).Include(d => d.Owner).OrderBy(d => d.Name).ToListAsync();
            return File("deals", ["name", "company", "contact", "owner", "value", "stage", "expected_close", "closed", "lost_reason", "source", "created"],
                deals.Select(d => new[]
                {
                    d.Name, d.Company?.Name, d.Contact?.Name, d.Owner?.Name, d.Value.ToString("0.##", CultureInfo.InvariantCulture), d.Stage.ToString(),
                    Day(d.ExpectedClose), d.Closed is { } closed ? Day(closed) : null, d.LostReason is { } why ? Names.Of(why) : null,
                    d.Source is { } source ? Names.Of(source) : null, Day(d.Created),
                }));
        }).RequireAuthorization(Policies.ViewRecords);
    }

    // A date any spreadsheet reads the same way: 2026-11-21.
    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // The file, named for what it holds and the day: contacts-2026-11-21.csv. It starts with a byte order mark,
    // which Excel needs to read names with accents.
    private static IResult File(string name, string[] columns, IEnumerable<string?[]> rows)
    {
        var csv = new StringBuilder();
        csv.AppendJoin(',', columns).Append("\r\n");
        foreach (var row in rows) csv.AppendJoin(',', row.Select(Cell)).Append("\r\n");
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return Results.File(bytes, "text/csv; charset=utf-8", $"{name}-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    // A cell: quoted when it has a comma, a quote or a line break. One a spreadsheet would run as a formula
    // (it starts with =, +, - or @) gets an apostrophe first, so it stays text.
    private static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return value.AsSpan().IndexOfAny(",\"\r\n") < 0 ? value : $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
