using Microsoft.EntityFrameworkCore;

namespace {{RootNamespace}}.Data;

/// <summary>
/// Importing companies (Data/Import.cs): a file needs a name column; website, industry, size, city and country
/// are read when they're there. A row matches the company with its name, whatever its capitals. A new company
/// is a prospect owned by the import's owner. An existing one, when the import updates, takes what the file has
/// and keeps the rest. Industry, size and country are lists in the app: an industry or country that isn't on
/// its list is saved as Other, and a size that isn't one of the four is left empty.
/// </summary>
public static class CompanyImport
{
    /// <summary>The column a file can't do without, as the dialog names it.</summary>
    internal static readonly string[] Needed = ["name"];

    /// <summary>Other names spreadsheets and exports give the columns, as <see cref="Import.Key"/> makes them.</summary>
    internal static readonly Dictionary<string, string> Aliases = new()
    {
        ["company"] = "name", ["companyname"] = "name", ["organization"] = "name", ["account"] = "name",
        ["url"] = "website", ["domain"] = "website", ["sector"] = "industry",
        ["companysize"] = "size", ["employees"] = "size", ["town"] = "city",
    };

    internal static async Task<ImportPlan> PlanAsync(Import import, ApplicationDbContext db)
    {
        var companies = new Dictionary<string, int>();
        foreach (var company in await db.Companies.AsNoTracking().Select(c => new { c.Id, c.Name }).ToListAsync()) companies.TryAdd(company.Name.ToLowerInvariant(), company.Id);

        var rows = import.Read(Aliases).Rows;
        var seen = new Dictionary<string, int>();
        foreach (var row in rows)
        {
            var name = row["name"]?.ToLowerInvariant() ?? "";
            string?[] facts = [IndustryOf(row) is { } industry ? Names.Of(industry) : null, SizeOf(row) is { } size ? Names.Of(size) : null];
            string?[] place = [row["city"], CountryOf(row)];
            (row.Name, row.Detail) = (row["name"] ?? "", row["website"]);
            (row.Aside, row.AsideNote) = (Joined(" · ", facts), Joined(", ", place));
            row.Problem = Check(row) ?? (seen.TryGetValue(name, out var first) ? $"Same name as row {first}" : null);
            if (row.Problem is not null)
            {
                row.Result = ImportResult.LeftOut;
                continue;
            }

            seen[name] = row.Number;
            if (companies.TryGetValue(name, out var id)) (row.ExistingId, row.Result) = (id, import.Match == ImportMatch.Update ? ImportResult.Updated : ImportResult.Skipped);
        }
        return new ImportPlan(rows);
    }

    internal static async Task ApplyAsync(Import import, ImportPlan plan, ApplicationDbContext db, DateOnly today)
    {
        var ids = plan.Incoming.Where(r => r.ExistingId is not null).Select(r => r.ExistingId!.Value).ToList();
        var existing = await db.Companies.Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        foreach (var row in plan.Incoming)
        {
            var company = row.ExistingId is { } id ? existing.GetValueOrDefault(id)
                : new Company { Name = row["name"]!, OwnerId = import.OwnerId, Status = CompanyStatus.Prospect, Added = today };
            if (company is null) continue;
            (company.Website, company.City, company.Country) = (row["website"] ?? company.Website, row["city"] ?? company.City, CountryOf(row) ?? company.Country);
            (company.Industry, company.Size) = (IndustryOf(row) ?? company.Industry, SizeOf(row) ?? company.Size);
            if (row.ExistingId is null) db.Companies.Add(company);
        }
    }

    // Why a row can't be a company, if it can't: the company form's rules (Data/Company.cs).
    private static string? Check(ImportRow row) =>
        row["name"] is not { } name ? "No company name"
        : name.Length > 120 ? "Company name is too long"
        : row["website"]?.Length > 200 ? "Website is too long"
        : row["city"]?.Length > 80 ? "City is too long"
        : null;

    // The industry a row names, by the list's own words ("Design & technology", "retail"); anything else is Other.
    private static Industry? IndustryOf(ImportRow row) => row["industry"] is not { } text ? null
        : Enum.GetValues<Industry>().Cast<Industry?>().FirstOrDefault(i => Import.Key(Names.Of(i!.Value)) == Import.Key(text)) ?? Industry.Other;

    // The size a row gives: one of the list's ranges ("11-50", "11–50 people", "201+") or a head count ("35").
    private static CompanySize? SizeOf(ImportRow row)
    {
        if (row["size"] is not { } text) return null;
        var digits = new string([.. text.Where(char.IsAsciiDigit)]);
        if (text.Trim().All(char.IsAsciiDigit) && int.TryParse(digits, out var people))
        {
            return people switch { <= 10 => CompanySize.UpTo10, <= 50 => CompanySize.UpTo50, <= 200 => CompanySize.UpTo200, _ => CompanySize.Over200 };
        }
        return digits switch { "110" => CompanySize.UpTo10, "1150" => CompanySize.UpTo50, "51200" => CompanySize.UpTo200, "201" => CompanySize.Over200, _ => null };
    }

    // The country a row names, as the list has it; one that isn't on the list is Other.
    private static string? CountryOf(ImportRow row) => row["country"] is not { } text ? null : Import.Key(text) switch
    {
        "us" or "usa" or "unitedstatesofamerica" => "United States",
        "uk" or "gb" or "greatbritain" => "United Kingdom",
        var key => Company.Countries.FirstOrDefault(country => Import.Key(country) == key) ?? "Other",
    };

    private static string? Joined(string separator, string?[] parts) => string.Join(separator, parts.Where(part => part is not null)) is { Length: > 0 } text ? text : null;
}
