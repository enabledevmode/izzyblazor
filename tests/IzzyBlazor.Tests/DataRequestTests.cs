using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IzzyBlazor.Tests;

// The request: applying it (with the component's fields or the server's), the result helpers,
// and its JSON, query-string and OData forms.
public sealed class DataRequestTests : IDisposable
{
    private static readonly Member[] Members =
    [
        new() { Id = 1, Name = "Ada", Email = "ada@example.com", Qty = 2, Tier = Tier.Gold, Active = true, Joined = new(2026, 1, 1), Secret = "s1" },
        new() { Id = 2, Name = "Grace", Email = "grace@navy.mil", Qty = 3, Tier = Tier.Basic, Joined = new(2026, 1, 2), Secret = "s2" },
        new() { Id = 3, Name = "adam", Email = null, Qty = 1, Tier = Tier.Platinum, Active = true, Joined = new(2026, 1, 3), Secret = "s3" },
        new() { Id = 4, Name = "Alan", Email = "alan@example.com", Qty = 3, Tier = Tier.Gold, Joined = new(2026, 1, 4), Secret = "s4" },
        new() { Id = 5, Name = "Katherine", Email = "kj@nasa.gov", Qty = 5, Tier = Tier.Basic, Active = true, Joined = new(2026, 1, 5), Secret = "s5" },
    ];

    private static readonly IReadOnlyList<DataField<Member>> Fields = DataFields.Of<Member>();

    private readonly SqliteConnection _connection;
    private readonly MemberDb _db;

    public DataRequestTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new MemberDb(new DbContextOptionsBuilder<MemberDb>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.Members.AddRange(Members.Select(m => new Member { Id = m.Id, Name = m.Name, Email = m.Email, Qty = m.Qty, Tier = m.Tier, Active = m.Active, Joined = m.Joined, Secret = m.Secret }));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static int[] Ids(IEnumerable<Member> members) => members.Select(m => m.Id).ToArray();

    // A request as a component makes it: with its fields attached.
    private static DataRequest<Member> FromComponent(DataRequest<Member> request) => request with { Fields = Fields };

    [Fact]
    public void Apply_filters_searches_sorts_and_takes_the_range()
    {
        var request = FromComponent(new DataRequest<Member>
        {
            Filter = Filter.Where("qty", FilterOperator.GreaterThanOrEqual, 2),
            Sort = [new("qty", Descending: true), new("name")],
            Skip = 1,
            Take = 2,
        });

        Assert.Equal(new[] { 4, 2 }, Ids(request.Apply(Members.AsQueryable())));
        Assert.Equal(new[] { 4, 2 }, Ids(request.Apply(_db.Members.AsNoTracking()).ToList()));
        Assert.Equal(4, request.ApplyFiltering(Members.AsQueryable()).Count());
        Assert.Equal(new[] { 5, 4, 2, 1 }, Ids(request.ApplySorting(request.ApplyFiltering(Members.AsQueryable()))));
    }

    [Theory]
    [InlineData("ad", new[] { 1, 3 })]
    [InlineData("NASA", new[] { 5 })]
    [InlineData("  example  ", new[] { 1, 4 })]
    [InlineData("nobody", new int[0])]
    [InlineData("", new[] { 1, 2, 3, 4, 5 })]
    public void Search_looks_in_the_text_fields(string search, int[] expected)
    {
        var request = FromComponent(new DataRequest<Member> { Search = search });

        Assert.Equal(expected, Ids(request.Apply(Members.AsQueryable())));
        Assert.Equal(expected, Ids(request.Apply(_db.Members.AsNoTracking()).ToList()));
    }

    [Fact]
    public void Search_with_no_searchable_field_matches_nothing()
    {
        var fields = new[] { new DataField<Member> { Key = "qty", Value = (Expression<Func<Member, int>>)(m => m.Qty), Searchable = true } };

        Assert.Empty(new DataRequest<Member> { Search = "3" }.Apply(Members.AsQueryable(), fields));
    }

    [Fact]
    public void A_request_without_fields_says_how_to_apply_it()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new DataRequest<Member>().Apply(Members.AsQueryable()));

        Assert.Contains("DataFields.Of<Member>()", ex.Message);
    }

    [Fact]
    public void On_a_server_keys_map_through_the_declared_fields_only()
    {
        var declared = Fields.Where(f => f.Key is "name" or "qty").ToList();
        var request = DataRequest<Member>.Parse("sort=secret,-qty&filter=secret eq 's1' or qty ge 3");

        // The sort by a field that isn't declared is left out; the condition on one matches nothing.
        Assert.Equal(new[] { 5, 2, 4 }, Ids(request.Apply(Members.AsQueryable(), declared)));
        Assert.Equal(new[] { 5, 2, 4 }, Ids(request.Apply(_db.Members.AsNoTracking(), declared).ToList()));

        // Neither does a field that's declared but not filterable or sortable.
        var readOnly = new[] { new DataField<Member> { Key = "secret", Value = (Expression<Func<Member, string>>)(m => m.Secret) } };
        Assert.Empty(DataRequest<Member>.Parse("filter=secret eq 's1'").Apply(Members.AsQueryable(), readOnly));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Ids(DataRequest<Member>.Parse("sort=-secret").Apply(Members.AsQueryable(), readOnly)));
    }

    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, null)]
    public async Task ToResultAsync_counts_only_when_the_total_is_needed(bool needsTotal, int? total)
    {
        var counted = 0;
        var request = FromComponent(new DataRequest<Member>
        {
            Filter = Filter.Where("qty", FilterOperator.GreaterThan, 1),
            Sort = [new("name")],
            Take = 2,
            NeedsTotal = needsTotal,
        });

        var result = await request.ToResultAsync(_db.Members.AsNoTracking(),
            count: (q, ct) => { counted++; return q.CountAsync(ct); },
            list: (q, ct) => q.ToListAsync(ct));

        Assert.Equal(new[] { 1, 4 }, Ids(result.Items));
        Assert.Equal(total, result.Total);
        Assert.Equal(needsTotal ? 1 : 0, counted);

        var sync = request.ToResult(Members.AsQueryable());
        Assert.Equal(new[] { 1, 4 }, Ids(sync.Items));
        Assert.Equal(total, sync.Total);
    }

    [Fact]
    public async Task ToResultAsync_passes_the_request_cancellation_on()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var request = FromComponent(new DataRequest<Member> { CancellationToken = cts.Token });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.ToResultAsync(_db.Members.AsNoTracking(),
            count: (q, ct) => q.CountAsync(ct), list: (q, ct) => q.ToListAsync(ct)));
    }

    private static DataRequest<Member> Full => new()
    {
        Sort = [new("tier"), new("joined", Descending: true)],
        Filter = Filter.All(
            Filter.Any(Filter.Where("name", FilterOperator.Contains, "a&b=c"), Filter.In("tier", [Tier.Gold, "Platinum"])),
            Filter.Between("qty", 2, null),
            Filter.Not(Filter.Where("email", FilterOperator.IsNull)),
            Filter.Where("joined", FilterOperator.OnDay, new DateOnly(2026, 1, 4))),
        Search = "ex ample+",
        Skip = 20,
        Take = 10,
        Cursor = "c/2+=",
        NeedsTotal = false,
    };

    [Fact]
    public void Json_round_trips_the_request()
    {
        var json = JsonSerializer.Serialize(Full);
        var back = JsonSerializer.Deserialize<DataRequest<Member>>(json)!;

        Assert.DoesNotContain("Page", json, StringComparison.Ordinal);
        Assert.DoesNotContain("CancellationToken", json, StringComparison.Ordinal);
        Assert.Equal(Full.Sort, back.Sort);
        Assert.Equal((Full.Search, Full.Skip, Full.Take, Full.Cursor, Full.NeedsTotal), (back.Search, back.Skip, back.Take, back.Cursor, back.NeedsTotal));
        Assert.Equal(Ids(Full.Apply(Members.AsQueryable(), Fields)), Ids(back.Apply(Members.AsQueryable(), Fields)));
        Assert.Equal(JsonSerializer.Serialize(back), json);
    }

    [Fact]
    public void The_query_string_round_trips_the_request()
    {
        var query = Full.ToQueryString();
        var back = DataRequest<Member>.Parse("https://api.example.com/members?" + query);

        Assert.StartsWith("sort=tier%2C-joined&skip=20&take=10&filter=", query, StringComparison.Ordinal);
        Assert.EndsWith("&search=ex%20ample%2B&cursor=c%2F2%2B%3D&total=false", query, StringComparison.Ordinal);
        Assert.Equal(Full with { Filter = back.Filter }, back);
        Assert.Equal(Full.Filter!.ToString(), back.Filter!.ToString());
        Assert.Equal(new DataRequest<Member>(), DataRequest<Member>.Parse(new DataRequest<Member>().ToQueryString()));
    }

    [Theory]
    [InlineData("skip=-5&take=x", 0, null)]
    [InlineData("?skip=40&take=20", 40, 20)]
    public void Numbers_that_dont_parse_are_left_out(string query, int skip, int? take)
    {
        var request = DataRequest<Member>.Parse(query);

        Assert.Equal((skip, take), (request.Skip, request.Take));
    }

    [Fact]
    public void A_filter_that_doesnt_parse_matches_nothing()
    {
        var request = DataRequest<Member>.Parse("filter=qty%20eq");

        Assert.Empty(request.Apply(Members.AsQueryable(), Fields));
    }

    [Fact]
    public void ToODataQuery_writes_OData_that_the_filter_parser_reads_back()
    {
        var request = Full with { NeedsTotal = true, Filter = Filter.All(Full.Filter!, Filter.Where("customer.name", FilterOperator.StartsWith, "O'B"), new FieldFilter("email", FilterOperator.EndsWith, ".GOV", MatchCase: true)) };
        var odata = request.ToODataQuery();
        var parts = QueryString.Parse(odata);

        Assert.Equal("tier,joined desc", parts["$orderby"].ToString());
        Assert.Equal(("20", "10", "true", "ex ample+"), (parts["$skip"].ToString(), parts["$top"].ToString(), parts["$count"].ToString(), parts["$search"].ToString()));
        var filter = parts["$filter"].ToString();
        Assert.Equal(
            "(contains(tolower(name),'a&b=c') or tier in ('Gold','Platinum')) and qty ge 2 and not (email eq null) and date(joined) eq 2026-01-04"
            + " and startswith(tolower(customer/name),'o''b') and endswith(email,'.GOV')",
            filter);

        var back = Filter.Parse(filter);
        Assert.Equal(Ids(new DataRequest<Member> { Filter = request.Filter }.Apply(Members.AsQueryable(), Fields)),
            Ids(new DataRequest<Member> { Filter = back }.Apply(Members.AsQueryable(), Fields)));
        Assert.Equal("customer.name", back.Find("customer.name")!.Field);
    }

    [Fact]
    public void Page_is_the_range_as_a_page()
    {
        Assert.Equal(1, new DataRequest<Member>().Page);
        Assert.Equal(1, new DataRequest<Member> { Skip = 9, Take = 10 }.Page);
        Assert.Equal(3, new DataRequest<Member> { Skip = 20, Take = 10 }.Page);
    }

    [Fact]
    public void Sort_reads_the_address_form()
    {
        Assert.Equal(new[] { new SortField("customer"), new SortField("total", true) }, SortField.ParseList(" customer , -total,,-"));
        Assert.Equal("-total", new SortField("total", true).ToString());
    }
}
