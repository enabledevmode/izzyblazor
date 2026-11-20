using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace {{RootNamespace}}.Data;

/// <summary>
/// A CSV of contacts someone has uploaded and not imported yet: Import contacts saves it, the preview page reads
/// it back, and importing deletes it. One left behind goes the next time anyone uploads, a day later.
/// The file needs first_name, last_name and email columns; company and phone are read when they're there.
/// </summary>
public class ContactImport
{
    /// <summary>The largest file taken, in bytes: what the upload says ("up to 1 MB").</summary>
    public const long MaxBytes = 1_000_000;

    /// <summary>The most contacts in one file.</summary>
    public const int MaxRows = 5000;

    public int Id { get; set; }

    [Required, StringLength(200)] public string FileName { get; set; } = "";

    /// <summary>The file's text.</summary>
    public string Csv { get; set; } = "";

    /// <summary>What happens to a row whose email address a contact already has.</summary>
    public ImportMatch Match { get; set; }

    /// <summary>Who owns the contacts and companies it adds.</summary>
    public string? OwnerId { get; set; }
    public ApplicationUser? Owner { get; set; }

    /// <summary>Who uploaded it: only they see its preview.</summary>
    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    /// <summary>When it was uploaded, in UTC.</summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Reads an uploaded file: text, of <see cref="MaxBytes"/> at most, whose first row names the columns.
    /// Null with a <paramref name="problem"/> to show when it can't be imported at all.
    /// </summary>
    public static async Task<(ContactImport? Import, string? Problem)> ReadAsync(IFormFile? file)
    {
        if (file is not { Length: > 0 }) return (null, "Choose a CSV file.");
        if (file.Length > MaxBytes) return (null, "Choose a file of 1 MB or less.");
        // UTF-8, or what the file's byte order mark says.
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync();
        if (text.Contains('\0')) return (null, "Choose a CSV file. This one isn’t text.");

        var name = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        var import = new ContactImport { FileName = name.Length is > 0 and <= 200 ? name : "Contacts.csv", Csv = text };
        var (columns, records) = import.Read();
        string[] missing = [.. Needed.Where(column => !columns.ContainsKey(column.Key)).Select(column => column.Name)];
        if (missing.Length > 0) return (null, $"The first row needs first_name, last_name and email columns. Missing: {string.Join(", ", missing)}.");
        if (records.Count == 0) return (null, "The file has column names and no contacts under them.");
        if (records.Count > MaxRows) return (null, $"Import {MaxRows:N0} contacts or fewer at a time. This file has {records.Count:N0}.");
        return (import, null);
    }

    /// <summary>
    /// What importing would do, row by row, against the contacts and companies there are now. A row is left out
    /// when a name or the email is missing, something doesn't fit its field, or an earlier row has its email.
    /// </summary>
    public async Task<ImportPlan> PlanAsync(ApplicationDbContext db)
    {
        // Email addresses and company names match whatever their capitals.
        var contacts = new Dictionary<string, int>();
        foreach (var contact in await db.Contacts.AsNoTracking().Select(c => new { c.Id, c.Email }).ToListAsync()) contacts.TryAdd(contact.Email.ToLowerInvariant(), contact.Id);
        var companies = new Dictionary<string, int>();
        foreach (var company in await db.Companies.AsNoTracking().Select(c => new { c.Id, c.Name }).ToListAsync()) companies.TryAdd(company.Name.ToLowerInvariant(), company.Id);

        var (columns, records) = Read();
        var rows = new List<ImportRow>();
        var seen = new Dictionary<string, int>();
        foreach (var (number, cells) in records)
        {
            string? Cell(string key) => columns.TryGetValue(key, out var at) && at < cells.Count && cells[at].Trim() is { Length: > 0 } value ? value : null;
            var row = new ImportRow
            {
                Number = number, FirstName = Cell("firstname") ?? "", LastName = Cell("lastname") ?? "", Email = Cell("email") ?? "",
                Company = Cell("company"), Phone = Cell("phone"),
            };
            var email = row.Email.ToLowerInvariant();
            row.Problem = Check(row) ?? (seen.TryGetValue(email, out var first) ? $"Same email as row {first}" : null);
            if (row.Problem is not null) row.Result = ImportResult.LeftOut;
            else
            {
                seen[email] = number;
                if (contacts.TryGetValue(email, out var id)) (row.ContactId, row.Result) = (id, Match == ImportMatch.Update ? ImportResult.Updated : ImportResult.Skipped);
                if (row.Company is { } company && row.Result != ImportResult.Skipped)
                {
                    row.CompanyId = companies.GetValueOrDefault(company.ToLowerInvariant()) is > 0 and var known ? known : null;
                    row.NewCompany = row.CompanyId is null;
                }
            }
            rows.Add(row);
        }
        return new ImportPlan(rows);
    }

    /// <summary>
    /// Imports it, all or nothing: adds the companies the file names that aren't here yet, then the new contacts
    /// (leads, owned by <see cref="Owner"/>), updates existing contacts if that was chosen, and deletes this
    /// upload. An existing contact takes the file's names, and its phone and company where the file has them.
    /// The audit log records each contact and company as it saves (Auditing/AuditInterceptor.cs).
    /// </summary>
    public async Task<ImportPlan> RunAsync(ApplicationDbContext db)
    {
        var plan = await PlanAsync(db);
        var today = Clock.Today(await db.Workspaces.AsNoTracking().FirstOrDefaultAsync());
        var incoming = plan.Rows.Where(r => r.Result is ImportResult.New or ImportResult.Updated).ToList();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // The companies first, one of each however many rows name it, so the contacts are saved (and logged)
        // with their companies' ids.
        var added = new Dictionary<string, Company>();
        foreach (var name in incoming.Where(r => r.NewCompany).Select(r => r.Company!))
        {
            added.TryAdd(name.ToLowerInvariant(), new Company { Name = name, OwnerId = OwnerId, Status = CompanyStatus.Prospect, Added = today });
        }
        db.Companies.AddRange(added.Values);
        await db.SaveChangesAsync();

        var ids = incoming.Where(r => r.ContactId is not null).Select(r => r.ContactId!.Value).ToList();
        var existing = await db.Contacts.Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        foreach (var row in incoming)
        {
            var contact = row.ContactId is { } id ? existing.GetValueOrDefault(id) : new Contact { Email = row.Email, OwnerId = OwnerId, Status = ContactStatus.Lead, Added = today };
            if (contact is null) continue;
            (contact.FirstName, contact.LastName, contact.Phone) = (row.FirstName, row.LastName, row.Phone ?? contact.Phone);
            if (row.Company is { } company) contact.CompanyId = row.CompanyId ?? added[company.ToLowerInvariant()].Id;
            if (row.ContactId is null) db.Contacts.Add(contact);
        }

        db.ContactImports.Remove(this);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return plan;
    }

    // The columns a file can't do without, as the form names them.
    private static readonly (string Key, string Name)[] Needed = [("firstname", "first_name"), ("lastname", "last_name"), ("email", "email")];

    // Other names spreadsheets and exports give the same columns, written as Key() makes them.
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["first"] = "firstname", ["givenname"] = "firstname", ["last"] = "lastname", ["surname"] = "lastname", ["familyname"] = "lastname",
        ["emailaddress"] = "email", ["companyname"] = "company", ["organization"] = "company", ["phonenumber"] = "phone", ["mobile"] = "phone",
    };

    // A column's name without capitals, spaces or underscores: "First Name" and "first_name" are both "firstname".
    private static string Key(string header)
    {
        var key = new string([.. header.Where(char.IsLetterOrDigit)]).ToLowerInvariant();
        return Aliases.GetValueOrDefault(key, key);
    }

    // Why a row can't be a contact, if it can't: the same rules as the contact form's (Data/Contact.cs).
    private static string? Check(ImportRow row) =>
        row.FirstName.Length == 0 ? "No first name"
        : row.LastName.Length == 0 ? "No last name"
        : row.Email.Length == 0 ? "No email address"
        : !new EmailAddressAttribute().IsValid(row.Email) ? "Not an email address"
        : row.Phone is not null && !new PhoneAttribute().IsValid(row.Phone) ? "Not a phone number"
        : row.FirstName.Length > 60 || row.LastName.Length > 60 ? "Name is too long"
        : row.Email.Length > 256 ? "Email address is too long"
        : row.Phone?.Length > 40 ? "Phone number is too long"
        : row.Company?.Length > 120 ? "Company name is too long"
        : null;

    // The file as its columns (by Key, to where each is in a row) and its rows, each with its number as a
    // spreadsheet shows it: the column names are row 1. Empty rows are passed over.
    private (Dictionary<string, int> Columns, List<(int Number, List<string> Cells)> Records) Read()
    {
        var all = Parse(Csv);
        var columns = new Dictionary<string, int>();
        if (all.Count == 0) return (columns, []);
        for (var at = 0; at < all[0].Count; at++) columns.TryAdd(Key(all[0][at]), at);
        return (columns, [.. all.Skip(1).Select((cells, at) => (Number: at + 2, Cells: cells)).Where(row => row.Cells.Any(cell => cell.Trim().Length > 0))]);
    }

    // CSV as spreadsheets write it: commas between values (semicolons or tabs, if that's what the first row
    // has), and quotes around a value with a comma, a line break or a doubled quote in it.
    private static List<List<string>> Parse(string text)
    {
        var first = text.AsSpan(0, text.IndexOfAny(['\r', '\n']) is >= 0 and var end ? end : text.Length);
        var separator = first.Contains(',') ? ',' : first.Contains(';') ? ';' : first.Contains('\t') ? '\t' : ',';
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var at = 0; at < text.Length; at++)
        {
            var c = text[at];
            if (quoted)
            {
                if (c != '"') cell.Append(c);
                else if (at + 1 < text.Length && text[at + 1] == '"') { cell.Append('"'); at++; }
                else quoted = false;
            }
            else if (c == '"' && cell.Length == 0) quoted = true;
            else if (c == separator) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && at + 1 < text.Length && text[at + 1] == '\n') at++;
                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = [];
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }
        return rows;
    }
}

/// <summary>What to do with a row whose email address a contact already has.</summary>
public enum ImportMatch
{
    [Display(Name = "Skip the existing contact")] Skip,
    [Display(Name = "Update the existing contact")] Update,
}

/// <summary>What importing does with a row.</summary>
public enum ImportResult
{
    [Display(Name = "New")] New,
    [Display(Name = "Updated")] Updated,
    [Display(Name = "Skipped")] Skipped,
    [Display(Name = "Left out")] LeftOut,
}

/// <summary>A row of an import's file, and what importing does with it.</summary>
public sealed class ImportRow
{
    /// <summary>Its row in the file, as a spreadsheet numbers it.</summary>
    public int Number { get; set; }

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Company { get; set; }
    public string? Phone { get; set; }

    public ImportResult Result { get; set; }

    /// <summary>Why it's left out.</summary>
    public string? Problem { get; set; }

    /// <summary>The contact that has its email address already.</summary>
    public int? ContactId { get; set; }

    /// <summary>Its company, when that's one already here.</summary>
    public int? CompanyId { get; set; }

    /// <summary>Whether importing adds its company.</summary>
    public bool NewCompany { get; set; }

    public string Name => $"{FirstName} {LastName}".Trim();
}

/// <summary>An import's rows, with what importing does with each.</summary>
public sealed record ImportPlan(IReadOnlyList<ImportRow> Rows)
{
    public int Count(ImportResult result) => Rows.Count(row => row.Result == result);

    /// <summary>How many companies it adds.</summary>
    public int NewCompanies => Rows.Where(row => row.NewCompany).Select(row => row.Company!.ToLowerInvariant()).Distinct().Count();
}
