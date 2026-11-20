using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace {{RootNamespace}}.Data;

/// <summary>
/// A CSV of contacts or companies someone has uploaded and not imported yet: the import dialog saves it, the
/// preview page reads it back, and importing deletes it. One left behind goes the next time anyone uploads, a
/// day later. What each kind's columns are, and what importing does with a row, is in Data/ContactImport.cs and
/// Data/CompanyImport.cs; this is what they share: the upload, the CSV reader and the preview's rows.
/// </summary>
public class Import
{
    /// <summary>The largest file taken, in bytes: what the upload says ("up to 1 MB").</summary>
    public const long MaxBytes = 1_000_000;

    /// <summary>The most rows in one file.</summary>
    public const int MaxRows = 5000;

    public int Id { get; set; }

    /// <summary>What the file holds.</summary>
    public ImportKind Kind { get; set; }

    [Required, StringLength(200)] public string FileName { get; set; } = "";

    /// <summary>The file's text.</summary>
    public string Csv { get; set; } = "";

    /// <summary>What happens to a row that matches a record already here: a contact's email, a company's name.</summary>
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
    public static async Task<(Import? Import, string? Problem)> ReadAsync(IFormFile? file, ImportKind kind)
    {
        if (file is not { Length: > 0 }) return (null, "Choose a CSV file.");
        if (file.Length > MaxBytes) return (null, "Choose a file of 1 MB or less.");
        // UTF-8, or what the file's byte order mark says.
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync();
        if (text.Contains('\0')) return (null, "Choose a CSV file. This one isn’t text.");

        var name = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        var import = new Import { Kind = kind, FileName = name.Length is > 0 and <= 200 ? name : "Import.csv", Csv = text };
        var (needed, aliases) = kind == ImportKind.Companies ? (CompanyImport.Needed, CompanyImport.Aliases) : (ContactImport.Needed, ContactImport.Aliases);
        var (columns, rows) = import.Read(aliases);
        string[] missing = [.. needed.Where(column => !columns.Contains(Key(column)))];
        var what = kind.ToString().ToLowerInvariant();
        if (missing.Length > 0)
        {
            return (null, needed.Length == 1 ? $"The first row needs a {needed[0]} column."
                : $"The first row needs {string.Join(", ", needed[..^1])} and {needed[^1]} columns. Missing: {string.Join(", ", missing)}.");
        }
        if (rows.Count == 0) return (null, $"The file has column names and no {what} under them.");
        if (rows.Count > MaxRows) return (null, $"Import {MaxRows:N0} {what} or fewer at a time. This file has {rows.Count:N0}.");
        return (import, null);
    }

    /// <summary>What importing would do, row by row, against the records there are now.</summary>
    public Task<ImportPlan> PlanAsync(ApplicationDbContext db) =>
        Kind == ImportKind.Companies ? CompanyImport.PlanAsync(this, db) : ContactImport.PlanAsync(this, db);

    /// <summary>
    /// Imports it, all or nothing, and deletes this upload. The audit log records each contact and company as
    /// it saves (Auditing/AuditInterceptor.cs).
    /// </summary>
    public async Task<ImportPlan> RunAsync(ApplicationDbContext db)
    {
        var plan = await PlanAsync(db);
        var today = Clock.Today(await db.Workspaces.AsNoTracking().FirstOrDefaultAsync());
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (Kind == ImportKind.Companies) await CompanyImport.ApplyAsync(this, plan, db, today);
        else await ContactImport.ApplyAsync(this, plan, db, today);
        db.Imports.Remove(this);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return plan;
    }

    /// <summary>
    /// A column's name without capitals, spaces or underscores: "First Name" and "first_name" are both
    /// "firstname". It's how columns are matched, and how a cell is matched to a list's option.
    /// </summary>
    public static string Key(string text) => new string([.. text.Where(char.IsLetterOrDigit)]).ToLowerInvariant();

    /// <summary>
    /// The file's columns and its rows, each with its values by column and its number as a spreadsheet shows it:
    /// the column names are row 1. A column is known by its <see cref="Key"/>, or by the name the kind's
    /// <paramref name="aliases"/> give that. Empty rows are passed over.
    /// </summary>
    internal (HashSet<string> Columns, List<ImportRow> Rows) Read(IReadOnlyDictionary<string, string> aliases)
    {
        var all = Parse(Csv);
        if (all.Count == 0) return ([], []);
        string[] columns = [.. all[0].Select(header => aliases.GetValueOrDefault(Key(header), Key(header)))];
        var rows = new List<ImportRow>();
        for (var at = 1; at < all.Count; at++)
        {
            var values = new Dictionary<string, string>();
            for (var cell = 0; cell < all[at].Count && cell < columns.Length; cell++)
            {
                if (all[at][cell].Trim() is { Length: > 0 } value) values.TryAdd(columns[cell], value);
            }
            if (values.Count > 0) rows.Add(new ImportRow { Number = at + 1, Values = values });
        }
        return ([.. columns], rows);
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

/// <summary>What an import's file holds.</summary>
public enum ImportKind { Contacts, Companies }

/// <summary>What to do with a row that matches a record already here.</summary>
public enum ImportMatch { Skip, Update }

/// <summary>What importing does with a row.</summary>
public enum ImportResult
{
    New,
    Updated,
    Skipped,
    [Display(Name = "Left out")] LeftOut,
}

/// <summary>A row of an import's file, and what importing does with it.</summary>
public sealed class ImportRow
{
    /// <summary>Its row in the file, as a spreadsheet numbers it.</summary>
    public int Number { get; init; }

    /// <summary>Its values by column, trimmed; a column it leaves empty isn't there.</summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();

    /// <summary>A column's value, or null.</summary>
    public string? this[string column] => Values.GetValueOrDefault(column);

    /// <summary>Who or what it is, for the preview: a person's name, a company's.</summary>
    public string Name { get; set; } = "";

    /// <summary>The line under the name: an email address, a website.</summary>
    public string? Detail { get; set; }

    /// <summary>The preview's second column and the line under it: a contact's company, a company's industry and place.</summary>
    public string? Aside { get; set; }
    public string? AsideNote { get; set; }

    public ImportResult Result { get; set; }

    /// <summary>Why it's left out.</summary>
    public string? Problem { get; set; }

    /// <summary>The record it matches: the contact with its email, the company with its name.</summary>
    public int? ExistingId { get; set; }

    /// <summary>A contact's company, when that's one already here.</summary>
    public int? CompanyId { get; set; }

    /// <summary>Whether importing a contact adds its company.</summary>
    public bool NewCompany { get; set; }
}

/// <summary>An import's rows, with what importing does with each.</summary>
public sealed record ImportPlan(IReadOnlyList<ImportRow> Rows)
{
    public int Count(ImportResult result) => Rows.Count(row => row.Result == result);

    /// <summary>The rows it adds or updates.</summary>
    public IEnumerable<ImportRow> Incoming => Rows.Where(row => row.Result is ImportResult.New or ImportResult.Updated);

    /// <summary>How many companies an import of contacts adds.</summary>
    public int NewCompanies => Rows.Where(row => row.NewCompany).Select(row => row["company"]!.ToLowerInvariant()).Distinct().Count();
}
