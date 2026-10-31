using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IzzyBlazor.Tests;

public enum Tier
{
    Basic,

    [Display(Name = "Gold member")]
    Gold,

    Platinum,
}

public sealed class Member
{
    public int Id { get; set; }

    [Display(Name = "Full name")]
    public string Name { get; set; } = "";

    public string? Email { get; set; }

    public int Qty { get; set; }

    [DisplayFormat(DataFormatString = "{0:N1}")]
    public double Score { get; set; }

    public int? Rank { get; set; }

    public Tier Tier { get; set; }

    public bool Active { get; set; }

    [DataType(DataType.Date)]
    public DateTime Joined { get; set; }

    public DateOnly Birthday { get; set; }

    public DateTime? Left { get; set; }

    public Guid Key { get; set; }

    [ScaffoldColumn(false)]
    public string Secret { get; set; } = "";

    // Not a field: a filter can't compare it.
    public List<string> Tags { get; set; } = [];
}

// The same text under SQLite's two collations, for what FilterCase leaves to the database.
public sealed class Person
{
    public int Id { get; set; }

    public string Exact { get; set; } = "";

    public string AnyCase { get; set; } = "";

    public string? Maybe { get; set; }
}

public sealed class PersonDb(DbContextOptions<PersonDb> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();

    protected override void OnModelCreating(ModelBuilder model) => model.Entity<Person>().Property(p => p.AnyCase).UseCollation("NOCASE");
}

public sealed class MemberDb(DbContextOptions<MemberDb> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();

    protected override void OnModelCreating(ModelBuilder model) => model.Entity<Member>().Ignore(m => m.Tags);
}

// The filter tree, its expressions (in memory and through EF Core's SQLite provider, so each must
// translate), the ?filter= syntax, JSON, and fields from DataAnnotations.
public sealed class DataFilterTests : IDisposable
{
    private static readonly Member[] Members =
    [
        new() { Id = 1, Name = "Ada", Email = "ada@example.com", Qty = 2, Score = 4.5, Rank = 1, Tier = Tier.Gold, Active = true, Joined = new(2026, 1, 1, 9, 30, 0), Birthday = new(1990, 12, 10), Key = Guid.Parse("11111111-1111-1111-1111-111111111111") },
        new() { Id = 2, Name = "Grace", Email = null, Qty = 3, Score = 3.0, Rank = null, Tier = Tier.Basic, Active = false, Joined = new(2026, 1, 2, 23, 59, 0), Birthday = new(1985, 6, 9), Left = new(2026, 3, 1), Key = Guid.Parse("22222222-2222-2222-2222-222222222222") },
        new() { Id = 3, Name = "adam", Email = "", Qty = 1, Score = 5.0, Rank = 2, Tier = Tier.Platinum, Active = true, Joined = new(2026, 1, 3, 0, 0, 0), Birthday = new(2000, 1, 1), Key = Guid.Parse("33333333-3333-3333-3333-333333333333") },
        new() { Id = 4, Name = "O'Brien", Email = "ob@example.com", Qty = 10, Score = 1.25, Rank = 3, Tier = Tier.Basic, Active = true, Joined = new(2026, 2, 14, 12, 0, 0), Birthday = new(1970, 2, 14), Key = Guid.Parse("44444444-4444-4444-4444-444444444444") },
    ];

    private static readonly IReadOnlyList<DataField<Member>> Fields = DataFields.Of<Member>();

    private readonly SqliteConnection _connection;
    private readonly MemberDb _db;

    public DataFilterTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new MemberDb(new DbContextOptionsBuilder<MemberDb>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.Members.AddRange(Members.Select(m => new Member
        {
            Id = m.Id, Name = m.Name, Email = m.Email, Qty = m.Qty, Score = m.Score, Rank = m.Rank, Tier = m.Tier, Active = m.Active,
            Joined = m.Joined, Birthday = m.Birthday, Left = m.Left, Key = m.Key,
        }));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static int[] InMemory(Filter filter) =>
        new DataRequest<Member> { Filter = filter }.Apply(Members.AsQueryable(), Fields).Select(m => m.Id).OrderBy(i => i).ToArray();

    private int[] InSqlite(Filter filter) =>
        new DataRequest<Member> { Filter = filter }.Apply(_db.Members.AsNoTracking(), Fields).Select(m => m.Id).ToList().OrderBy(i => i).ToArray();

    private static DataField<Person>[] PeopleFields(FilterCase textCase) =>
    [
        new() { Key = "exact", Value = (Expression<Func<Person, string>>)(p => p.Exact), Filterable = true, Searchable = true, Case = textCase },
        new() { Key = "anycase", Value = (Expression<Func<Person, string>>)(p => p.AnyCase), Filterable = true, Case = textCase },
    ];

    private PersonDb People()
    {
        var db = new PersonDb(new DbContextOptionsBuilder<PersonDb>().UseSqlite(_connection).Options);
        db.Database.ExecuteSqlRaw("""CREATE TABLE IF NOT EXISTS "People" ("Id" INTEGER PRIMARY KEY, "Exact" TEXT NOT NULL, "AnyCase" TEXT NOT NULL COLLATE NOCASE, "Maybe" TEXT NULL)""");
        db.People.AddRange(new Person { Id = 1, Exact = "Ada", AnyCase = "Ada", Maybe = "Ada" }, new Person { Id = 2, Exact = "Grace", AnyCase = "Grace" });
        db.SaveChanges();
        return db;
    }

    // A null on the column: each mode keeps the same rows through EF as in memory, the operators that
    // keep nulls keep it, and only IgnoreCase lowers.
    [Theory]
    [InlineData(FilterCase.IgnoreCase, FilterOperator.Equals, "ADA", new[] { 1 })]
    [InlineData(FilterCase.IgnoreCase, FilterOperator.NotEquals, "ADA", new[] { 2 })]
    [InlineData(FilterCase.IgnoreCase, FilterOperator.DoesNotContain, "ADA", new[] { 2 })]
    [InlineData(FilterCase.Database, FilterOperator.Equals, "Ada", new[] { 1 })]
    [InlineData(FilterCase.Database, FilterOperator.NotEquals, "Ada", new[] { 2 })]
    [InlineData(FilterCase.Database, FilterOperator.Contains, "Ada", new[] { 1 })]
    [InlineData(FilterCase.Database, FilterOperator.DoesNotContain, "Ada", new[] { 2 })]
    [InlineData(FilterCase.MatchCase, FilterOperator.NotEquals, "ADA", new[] { 1, 2 })]
    [InlineData(FilterCase.MatchCase, FilterOperator.DoesNotContain, "ADA", new[] { 1, 2 })]
    public void FilterCase_on_a_nullable_column_keeps_the_same_rows_through_EF(FilterCase textCase, FilterOperator op, string value, int[] expected)
    {
        using var db = People();
        DataField<Person>[] fields = [new() { Key = "maybe", Value = (Expression<Func<Person, string?>>)(p => p.Maybe), Filterable = true, Case = textCase }];
        var memory = new[] { new Person { Id = 1, Exact = "Ada", AnyCase = "Ada", Maybe = "Ada" }, new Person { Id = 2, Exact = "Grace", AnyCase = "Grace" } };
        var request = new DataRequest<Person> { Filter = Filter.Where("maybe", op, value) };

        var query = request.Apply(db.People.AsNoTracking(), fields);
        Assert.Equal(expected, query.Select(p => p.Id).OrderBy(i => i).ToArray());
        Assert.Equal(expected, request.Apply(memory.AsQueryable(), fields).Select(p => p.Id).OrderBy(i => i).ToArray());
        Assert.Equal(textCase == FilterCase.IgnoreCase, query.ToQueryString().Contains("lower(", StringComparison.OrdinalIgnoreCase));
    }

    // "Matches nothing, whoever writes it": a field's own condition doesn't get round its operators.
    [Fact]
    public void A_fields_own_condition_doesnt_get_round_its_operators()
    {
        DataField<Member>[] fields =
        [
            new()
            {
                Key = "name",
                Value = (Expression<Func<Member, string>>)(m => m.Name),
                Filterable = true,
                Operators = [FilterOperator.Equals],
                Filter = _ => m => true,
            },
        ];

        int[] Ids(FilterOperator op) => new DataRequest<Member> { Filter = Filter.Where("name", op, "Ada") }.Apply(Members.AsQueryable(), fields).Select(m => m.Id).ToArray();

        Assert.Equal(Members.Length, Ids(FilterOperator.Equals).Length);
        Assert.Empty(Ids(FilterOperator.Contains));
    }

    // A number in a culture: as the person writes it when its groups are whole, else the invariant form.
    [Theory]
    [InlineData("de-DE", "1000.25", 1000.25)]
    [InlineData("de-DE", "1.5", 1.5)]
    [InlineData("de-DE", "1,5", 1.5)]
    [InlineData("de-DE", "1.234,5", 1234.5)]
    [InlineData("de-DE", "1.234", 1234.0)]
    [InlineData("de-DE", "1.234.567", 1234567.0)]
    [InlineData("en-US", "1,234.5", 1234.5)]
    [InlineData("en-US", "1000.25", 1000.25)]
    public void A_number_reads_by_its_culture_when_its_groups_are_whole(string culture, string text, double expected)
    {
        var was = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            Assert.True(FilterConvert.TryParse(typeof(decimal), text, out var value), text);
            Assert.Equal((decimal)expected, value);
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }

    [Fact]
    public void A_comma_that_isnt_a_group_isnt_a_number_in_english()
    {
        var was = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            Assert.False(FilterConvert.TryParse(typeof(decimal), "1,5", out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }

    public static TheoryData<FilterOperator, object> CaseConditions => new()
    {
        { FilterOperator.Equals, "ADA" },
        { FilterOperator.NotEquals, "ADA" },
        { FilterOperator.Contains, "ADA" },
        { FilterOperator.DoesNotContain, "ADA" },
        { FilterOperator.StartsWith, "ADA" },
        { FilterOperator.EndsWith, "ADA" },
        { FilterOperator.In, new List<object?> { "ADA", "x" } },
        { FilterOperator.NotIn, new List<object?> { "ADA", "x" } },
    };

    // Whatever the operator, Database and MatchCase write no LOWER and IgnoreCase does, and one
    // condition's MatchCase goes first.
    [Theory]
    [MemberData(nameof(CaseConditions))]
    public void FilterCase_decides_whether_the_sql_lowers_text(FilterOperator op, object value)
    {
        using var db = People();
        string Sql(FilterCase textCase, bool matchCase = false) =>
            new DataRequest<Person> { Filter = new FieldFilter("exact", op, value, MatchCase: matchCase) }.Apply(db.People.AsNoTracking(), PeopleFields(textCase)).ToQueryString();

        Assert.Contains("lower(", Sql(FilterCase.IgnoreCase), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lower(", Sql(FilterCase.Database), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lower(", Sql(FilterCase.MatchCase), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lower(", Sql(FilterCase.IgnoreCase, matchCase: true), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FilterCase_decides_whether_a_search_lowers_text()
    {
        using var db = People();
        string Sql(FilterCase textCase) => new DataRequest<Person> { Search = "ADA" }.Apply(db.People.AsNoTracking(), PeopleFields(textCase)).ToQueryString();
        int[] Ids(FilterCase textCase) => new DataRequest<Person> { Search = "ADA" }.Apply(db.People.AsNoTracking(), PeopleFields(textCase)).Select(p => p.Id).ToArray();

        Assert.Contains("lower(", Sql(FilterCase.IgnoreCase), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lower(", Sql(FilterCase.Database), StringComparison.OrdinalIgnoreCase);
        Assert.Equal([1], Ids(FilterCase.IgnoreCase));
    }

    // The rows: lowering finds it anywhere; with none, the column's collation decides, so only the
    // NOCASE column does; matching case is exact in memory.
    [Theory]
    [InlineData(FilterCase.IgnoreCase, "exact", new[] { 1 }, new[] { 1 })]
    [InlineData(FilterCase.IgnoreCase, "anycase", new[] { 1 }, new[] { 1 })]
    [InlineData(FilterCase.Database, "exact", new int[0], new int[0])]
    [InlineData(FilterCase.Database, "anycase", new[] { 1 }, new int[0])]
    [InlineData(FilterCase.MatchCase, "exact", new int[0], new int[0])]
    public void FilterCase_finds_what_it_says(FilterCase textCase, string field, int[] inSqlite, int[] inMemory)
    {
        using var db = People();
        var request = new DataRequest<Person> { Filter = Filter.Where(field, FilterOperator.Equals, "ADA") };
        var memory = new[] { new Person { Id = 1, Exact = "Ada", AnyCase = "Ada" }, new Person { Id = 2, Exact = "Grace", AnyCase = "Grace" } };

        Assert.Equal(inSqlite, request.Apply(db.People.AsNoTracking(), PeopleFields(textCase)).Select(p => p.Id).ToArray());
        Assert.Equal(inMemory, request.Apply(memory.AsQueryable(), PeopleFields(textCase)).Select(p => p.Id).ToArray());
    }

    [Theory]
    [InlineData(FilterCase.IgnoreCase, "tolower(exact)")]
    [InlineData(FilterCase.Database, "contains(exact,'Ada')")]
    [InlineData(FilterCase.MatchCase, "contains(exact,'Ada')")]
    public void An_odata_query_follows_the_fields_case(FilterCase textCase, string expected)
    {
        var request = new DataRequest<Person> { Filter = Filter.Where("exact", FilterOperator.Contains, "Ada") };

        Assert.Contains(expected, Uri.UnescapeDataString(request.ToODataQuery(PeopleFields(textCase))), StringComparison.Ordinal);
    }

    // A day on a field with a time means the whole day: rows around 3 Jan, with their ids in time order.
    private static readonly DateTime[] AroundTheDay =
    [
        new(2026, 1, 2, 23, 59, 0), new(2026, 1, 3, 0, 0, 0), new(2026, 1, 3, 9, 0, 0), new(2026, 1, 3, 23, 59, 59), new(2026, 1, 4, 0, 0, 0),
    ];

    private static readonly DateOnly TheDay = new(2026, 1, 3);

    public static TheoryData<string, Filter, int[]> Days() => new()
    {
        { "on or after", Filter.Where("joined", FilterOperator.GreaterThanOrEqual, TheDay), [2, 3, 4, 5] },
        { "before", Filter.Where("joined", FilterOperator.LessThan, TheDay), [1] },
        { "on or before", Filter.Where("joined", FilterOperator.LessThanOrEqual, TheDay), [1, 2, 3, 4] },
        { "after", Filter.Where("joined", FilterOperator.GreaterThan, TheDay), [5] },
        { "equals", Filter.Where("joined", FilterOperator.Equals, TheDay), [2, 3, 4] },
        { "not equals", Filter.Where("joined", FilterOperator.NotEquals, TheDay), [1, 5] },
        { "between", Filter.Between("joined", new DateOnly(2026, 1, 2), TheDay), [1, 2, 3, 4] },
        { "between, to the day only", Filter.Between("joined", null, TheDay), [1, 2, 3, 4] },
        { "between, from the day only", Filter.Between("joined", TheDay, null), [2, 3, 4, 5] },
        { "a day as text", Filter.Where("joined", FilterOperator.LessThanOrEqual, "2026-01-03"), [1, 2, 3, 4] },
        { "a time as text stays that instant", Filter.Where("joined", FilterOperator.LessThanOrEqual, "2026-01-03T00:00:00"), [1, 2] },
        { "a DateTime stays that instant, on or before", Filter.Where("joined", FilterOperator.LessThanOrEqual, new DateTime(2026, 1, 3)), [1, 2] },
        { "a DateTime stays that instant, after", Filter.Where("joined", FilterOperator.GreaterThan, new DateTime(2026, 1, 3)), [3, 4, 5] },
        { "the last day there is, on or before", Filter.Where("joined", FilterOperator.LessThanOrEqual, DateOnly.MaxValue), [1, 2, 3, 4, 5] },
        { "the last day there is, after", Filter.Where("joined", FilterOperator.GreaterThan, DateOnly.MaxValue), [] },
        { "a date field is unchanged", Filter.Where("birthday", FilterOperator.LessThanOrEqual, TheDay), [1, 2, 3, 4, 5] },
        { "nullable, on or before", Filter.Where("left", FilterOperator.LessThanOrEqual, TheDay), [1, 2, 3, 4] },
    };

    [Theory]
    [MemberData(nameof(Days))]
    public void A_day_on_a_field_with_a_time_means_the_whole_day(string what, Filter filter, int[] expected)
    {
        var rows = AroundTheDay.Select((at, i) => new Member { Id = i + 1, Name = "n", Joined = at, Left = at, Birthday = new DateOnly(2000, 1, 1) }).ToList();
        var inMemory = new DataRequest<Member> { Filter = filter }.Apply(rows.AsQueryable(), Fields).Select(m => m.Id).OrderBy(i => i).ToArray();
        Assert.True(expected.SequenceEqual(inMemory), $"{what} in memory: {string.Join(",", inMemory)}");

        _db.Members.RemoveRange(_db.Members);
        _db.SaveChanges();
        _db.Members.AddRange(rows);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        var inSqlite = InSqlite(filter);
        Assert.True(expected.SequenceEqual(inSqlite), $"{what} in SQLite: {string.Join(",", inSqlite)}");
    }

    public sealed class Stamped
    {
        public int Id { get; set; }

        public DateTimeOffset At { get; set; }
    }

    // The day on the clock where it was recorded, as OnDay reads it: 3 Jan 23:00 at -05:00 is 3 Jan, though it's 4 Jan in UTC.
    public static TheoryData<string, Filter, int[]> OffsetDays() => new()
    {
        { "on day", Filter.Where("at", FilterOperator.OnDay, TheDay), [2, 3] },
        { "equals", Filter.Where("at", FilterOperator.Equals, TheDay), [2, 3] },
        { "not equals", Filter.Where("at", FilterOperator.NotEquals, TheDay), [1, 4] },
        { "on or before", Filter.Where("at", FilterOperator.LessThanOrEqual, TheDay), [1, 2, 3] },
        { "after", Filter.Where("at", FilterOperator.GreaterThan, TheDay), [4] },
        { "on or after", Filter.Where("at", FilterOperator.GreaterThanOrEqual, TheDay), [2, 3, 4] },
        { "before", Filter.Where("at", FilterOperator.LessThan, TheDay), [1] },
        { "between", Filter.Between("at", TheDay, TheDay), [2, 3] },
    };

    [Theory]
    [MemberData(nameof(OffsetDays))]
    public void A_day_on_an_offset_field_is_the_day_on_its_clock(string what, Filter filter, int[] expected)
    {
        Stamped[] rows =
        [
            new() { Id = 1, At = new(2026, 1, 2, 23, 0, 0, TimeSpan.FromHours(-5)) },
            new() { Id = 2, At = new(2026, 1, 3, 1, 0, 0, TimeSpan.FromHours(5)) },
            new() { Id = 3, At = new(2026, 1, 3, 23, 0, 0, TimeSpan.FromHours(-5)) },
            new() { Id = 4, At = new(2026, 1, 4, 0, 30, 0, TimeSpan.Zero) },
        ];
        var ids = new DataRequest<Stamped> { Filter = filter }.Apply(rows.AsQueryable(), DataFields.Of<Stamped>()).Select(m => m.Id).OrderBy(i => i).ToArray();
        Assert.True(expected.SequenceEqual(ids), $"{what}: {string.Join(",", ids)}");
    }

    [Fact]
    public void In_odata_a_day_on_a_field_with_a_time_is_the_whole_day()
    {
        string OData(Filter filter) => Uri.UnescapeDataString(new DataRequest<Member> { Filter = filter }.ToODataQuery(Fields));

        Assert.Contains("joined lt 2026-01-04", OData(Filter.Where("joined", FilterOperator.LessThanOrEqual, TheDay)), StringComparison.Ordinal);
        Assert.Contains("joined ge 2026-01-04", OData(Filter.Where("joined", FilterOperator.GreaterThan, TheDay)), StringComparison.Ordinal);
        Assert.Contains("joined lt 2026-01-04", OData(Filter.Between("joined", new DateOnly(2026, 1, 2), TheDay)), StringComparison.Ordinal);
        Assert.Contains("joined le 2026-01-03T00:00:00", OData(Filter.Where("joined", FilterOperator.LessThanOrEqual, new DateTime(2026, 1, 3))), StringComparison.Ordinal);
    }

    // Each operator by type: the same ids in memory and in SQLite.
    public static TheoryData<string, Filter, int[]> Operators() => new()
    {
        { "text contains, ignoring case", Filter.Where("name", FilterOperator.Contains, "AD"), [1, 3] },
        { "text contains, matching case", new FieldFilter("name", FilterOperator.Contains, "Ad", MatchCase: true), [1] },
        { "text doesn't contain", Filter.Where("name", FilterOperator.DoesNotContain, "ad"), [2, 4] },
        { "text starts with", Filter.Where("name", FilterOperator.StartsWith, "a"), [1, 3] },
        { "text ends with", Filter.Where("name", FilterOperator.EndsWith, "CE"), [2] },
        { "text equals, ignoring case", Filter.Where("name", FilterOperator.Equals, "ADA"), [1] },
        { "text not equals", Filter.Where("name", FilterOperator.NotEquals, "ada"), [2, 3, 4] },
        { "text in", Filter.In("name", ["ada", "GRACE", "nobody"]), [1, 2] },
        { "text not in", new FieldFilter("name", FilterOperator.NotIn, new object?[] { "ada" }), [2, 3, 4] },
        { "text with a quote", Filter.Where("name", FilterOperator.Contains, "o'b"), [4] },
        { "text is null", Filter.Where("email", FilterOperator.IsNull), [2] },
        { "text is not null", Filter.Where("email", FilterOperator.IsNotNull), [1, 3, 4] },
        { "text is empty (or null)", Filter.Where("email", FilterOperator.IsEmpty), [2, 3] },
        { "text is not empty", Filter.Where("email", FilterOperator.IsNotEmpty), [1, 4] },
        { "number equals", Filter.Where("qty", FilterOperator.Equals, 3), [2] },
        { "number not equals", Filter.Where("qty", FilterOperator.NotEquals, 3), [1, 3, 4] },
        { "number greater than", Filter.Where("qty", FilterOperator.GreaterThan, 2), [2, 4] },
        { "number at least", Filter.Where("qty", FilterOperator.GreaterThanOrEqual, 2), [1, 2, 4] },
        { "number less than", Filter.Where("qty", FilterOperator.LessThan, 3), [1, 3] },
        { "number at most", Filter.Where("qty", FilterOperator.LessThanOrEqual, 3), [1, 2, 3] },
        { "number between", Filter.Between("qty", 2, 3), [1, 2] },
        { "number between, open end", Filter.Between("qty", 3, null), [2, 4] },
        { "number at least a fraction rounds up", Filter.Where("qty", FilterOperator.GreaterThanOrEqual, 2.5), [2, 4] },
        { "number greater than a fraction", Filter.Where("qty", FilterOperator.GreaterThan, 2.5), [2, 4] },
        { "number less than a fraction", Filter.Where("qty", FilterOperator.LessThan, 2.5), [1, 3] },
        { "number equals a fraction matches nothing", Filter.Where("qty", FilterOperator.Equals, 2.5), [] },
        { "number beyond the type: at most keeps all", Filter.Where("qty", FilterOperator.LessThanOrEqual, 1e12), [1, 2, 3, 4] },
        { "number beyond the type: at least keeps none", Filter.Where("qty", FilterOperator.GreaterThanOrEqual, 1e12), [] },
        { "number as text", Filter.Where("qty", FilterOperator.Equals, "10"), [4] },
        { "number that isn't one", Filter.Where("qty", FilterOperator.Equals, "ten"), [] },
        { "number in", Filter.In("qty", [1, "3", 99]), [2, 3] },
        { "double between", Filter.Between("score", 3, 4.5), [1, 2] },
        { "nullable number equals", Filter.Where("rank", FilterOperator.Equals, 2), [3] },
        { "nullable number is null", Filter.Where("rank", FilterOperator.IsNull), [2] },
        { "nullable number equals null", Filter.Where("rank", FilterOperator.Equals, null), [2] },
        { "nullable number greater than leaves out null", Filter.Where("rank", FilterOperator.GreaterThan, 0), [1, 3, 4] },
        { "nullable number in, with null", Filter.In("rank", [1, null]), [1, 2] },
        { "enum equals", Filter.Where("tier", FilterOperator.Equals, Tier.Gold), [1] },
        { "enum by name, ignoring case", Filter.Where("tier", FilterOperator.Equals, "basic"), [2, 4] },
        { "enum by number", Filter.Where("tier", FilterOperator.Equals, 2), [3] },
        { "enum not equals", Filter.Where("tier", FilterOperator.NotEquals, "Basic"), [1, 3] },
        { "enum in", Filter.In("tier", ["Gold", Tier.Platinum]), [1, 3] },
        { "enum not in", new FieldFilter("tier", FilterOperator.NotIn, new object?[] { "Gold", "Platinum" }), [2, 4] },
        { "enum that isn't one", Filter.Where("tier", FilterOperator.Equals, "Silver"), [] },
        { "bool equals", Filter.Where("active", FilterOperator.Equals, false), [2] },
        { "bool as text", Filter.Where("active", FilterOperator.Equals, "true"), [1, 3, 4] },
        { "date time on day", Filter.Where("joined", FilterOperator.OnDay, "2026-01-02"), [2] },
        { "date time on day, a DateOnly", Filter.Where("joined", FilterOperator.OnDay, new DateOnly(2026, 1, 3)), [3] },
        { "date time after", Filter.Where("joined", FilterOperator.GreaterThan, new DateTime(2026, 1, 2)), [2, 3, 4] },
        { "date time between days", Filter.Between("joined", new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 3)), [2, 3] },
        { "date only before", Filter.Where("birthday", FilterOperator.LessThan, "1986-01-01"), [2, 4] },
        { "date only on day", Filter.Where("birthday", FilterOperator.OnDay, "2000-01-01"), [3] },
        { "date that isn't one", Filter.Where("birthday", FilterOperator.OnDay, "someday"), [] },
        { "nullable date on day", Filter.Where("left", FilterOperator.OnDay, "2026-03-01"), [2] },
        { "nullable date is not null", Filter.Where("left", FilterOperator.IsNotNull), [2] },
        { "guid equals", Filter.Where("key", FilterOperator.Equals, "33333333-3333-3333-3333-333333333333"), [3] },
        { "and", Filter.All(Filter.Where("active", FilterOperator.Equals, true), Filter.Where("qty", FilterOperator.LessThan, 5)), [1, 3] },
        { "or", Filter.Any(Filter.Where("tier", FilterOperator.Equals, "Gold"), Filter.Where("qty", FilterOperator.Equals, 10)), [1, 4] },
        { "not", Filter.Not(Filter.Where("active", FilterOperator.Equals, true)), [2] },
        { "nested", Filter.All(Filter.Not(Filter.Any(Filter.Where("name", FilterOperator.StartsWith, "a"), Filter.Where("qty", FilterOperator.Equals, 10)))), [2] },
        { "an empty and keeps everything", Filter.All(), [1, 2, 3, 4] },
        { "an empty or keeps nothing", Filter.Any(), [] },
        { "an unknown field matches nothing", Filter.Where("salary", FilterOperator.GreaterThan, 0), [] },
        { "an unknown field inside not", Filter.Not(Filter.Where("salary", FilterOperator.GreaterThan, 0)), [1, 2, 3, 4] },
        { "an operator the type doesn't take", Filter.Where("qty", FilterOperator.Contains, "1"), [] },
        { "a left-out property", Filter.Where("secret", FilterOperator.IsNotNull), [] },
    };

    [Theory]
    [MemberData(nameof(Operators))]
    public void Each_operator_matches_the_same_rows_in_memory_and_in_sqlite(string what, Filter filter, int[] expected)
    {
        Assert.True(expected.SequenceEqual(InMemory(filter)), $"{what} in memory: {string.Join(",", InMemory(filter))}");
        Assert.True(expected.SequenceEqual(InSqlite(filter)), $"{what} in SQLite: {string.Join(",", InSqlite(filter))}");
    }

    [Fact]
    public void Text_values_parse_in_the_current_culture_then_the_invariant_form()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(new[] { 1, 2 }, InMemory(Filter.Between("score", "2,5", "4,5")));
            Assert.Equal(new[] { 4 }, InMemory(Filter.Where("score", FilterOperator.Equals, "1,25")));
            Assert.Equal(new[] { 1 }, InMemory(Filter.Where("birthday", FilterOperator.OnDay, "10.12.1990")));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }

        Assert.Equal(new[] { 4 }, InMemory(Filter.Where("score", FilterOperator.Equals, "1.25")));
    }

    [Fact]
    public void A_date_time_offset_on_day_is_the_day_on_its_own_clock()
    {
        var rows = new[] { (Id: 1, At: new DateTimeOffset(2026, 1, 2, 23, 0, 0, TimeSpan.FromHours(-4))), (Id: 2, At: new DateTimeOffset(2026, 1, 3, 1, 0, 0, TimeSpan.Zero)) };
        var fields = new[] { Field<(int Id, DateTimeOffset At), DateTimeOffset>("at", r => r.At) };
        var kept = new DataRequest<(int Id, DateTimeOffset At)> { Filter = Filter.Where("at", FilterOperator.OnDay, "2026-01-02") }
            .Apply(rows.AsQueryable(), fields).Select(r => r.Id);
        Assert.Equal(new[] { 1 }, kept);
    }

    [Fact]
    public void Find_reads_a_condition_every_match_meets()
    {
        var filter = Filter.All(Filter.Where("name", FilterOperator.Contains, "ad"), Filter.All(Filter.Between("qty", 1, 3)), Filter.Any(Filter.Where("tier", FilterOperator.Equals, "Gold")));

        Assert.Equal("ad", filter.Find("NAME")!.Value);
        Assert.Equal(FilterOperator.Between, filter.Find("qty")!.Operator);
        Assert.Null(filter.Find("tier"));
        Assert.Null(Filter.Not(Filter.Where("name", FilterOperator.Equals, "x")).Find("name"));
    }

    [Fact]
    public void Json_round_trips_the_tree_by_value()
    {
        var filter = Filter.All(
            Filter.Where("name", FilterOperator.Contains, "o'b"),
            new FieldFilter("email", FilterOperator.StartsWith, "OB", MatchCase: true),
            Filter.Any(Filter.Where("tier", FilterOperator.Equals, Tier.Gold), Filter.In("qty", [10, 3])),
            Filter.Not(Filter.Where("joined", FilterOperator.OnDay, new DateOnly(2026, 1, 2))),
            Filter.Between("score", 1, 4.75m),
            Filter.Where("key", FilterOperator.NotEquals, Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Filter.Where("left", FilterOperator.IsNull),
            Filter.Where("birthday", FilterOperator.LessThan, new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var json = JsonSerializer.Serialize<Filter>(filter);
        var back = JsonSerializer.Deserialize<Filter>(json)!;

        Assert.Contains("\"type\":\"group\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Operator\":\"Contains\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Value\":\"Gold\"", json, StringComparison.Ordinal);
        // Dates and Guids come back as text, which the field's type parses: the same rows match.
        Assert.Equal(InMemory(filter), InMemory(back));
        Assert.Equal(InSqlite(filter), InSqlite(back));
        Assert.Equal(JsonSerializer.Serialize<Filter>(back), json);
        Assert.True(((FieldFilter)((FilterGroup)back).Filters[1]).MatchCase);
    }

    [Theory]
    [InlineData("total ge 100", "total ge 100")]
    [InlineData("qty ge 2 and (tier in ('Gold','Platinum') or name contains 'ada')", "qty ge 2 and (tier in ('Gold','Platinum') or contains(name,'ada'))")]
    [InlineData("contains(name, 'ad') eq true", "contains(name,'ad')")]
    [InlineData("contains(tolower(name),'ad') eq false", "not contains(name,'ad')")]
    [InlineData("not contains(Name,'ad')", "not contains(Name,'ad')")]
    [InlineData("not (tier in ('Gold'))", "not (tier in ('Gold'))")]
    [InlineData("not (qty gt 2)", "not (qty gt 2)")]
    [InlineData("email eq null", "email eq null")]
    [InlineData("email ne null", "email ne null")]
    [InlineData("date(joined) eq 2026-01-02", "date(joined) eq 2026-01-02")]
    [InlineData("joined lt 2026-01-02T12:00:00Z", "joined lt 2026-01-02T12:00:00+00:00")]
    [InlineData("score le -1.5e2", "score le -150")]
    [InlineData("customer/name eq 'O''Brien'", "customer.name eq 'O''Brien'")]
    [InlineData("tier eq Namespace.Tier'Gold'", "tier eq 'Gold'")]
    [InlineData("key eq 11111111-1111-1111-1111-111111111111", "key eq 11111111-1111-1111-1111-111111111111")]
    [InlineData("true", "true")]
    [InlineData("a eq 1 or b eq 2 and c eq 3", "a eq 1 or (b eq 2 and c eq 3)")]
    public void Filter_text_parses_into_the_tree(string text, string written)
    {
        Assert.Equal(written, Filter.Parse(text).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("qty")]
    [InlineData("qty eq")]
    [InlineData("qty eq ada")]
    [InlineData("qty like 'a'")]
    [InlineData("(qty eq 1")]
    [InlineData("qty eq 1)")]
    [InlineData("name eq 'unclosed")]
    [InlineData("1qty eq 1")]
    [InlineData("qty eq 1; drop table members")]
    [InlineData("date(joined) gt 2026-01-01")]
    public void Filter_text_that_isnt_the_syntax_doesnt_parse(string text)
    {
        Assert.False(Filter.TryParse(text, out _));
        if (text.Length > 0)
        {
            Assert.Throws<FormatException>(() => Filter.Parse(text));
        }
    }

    [Fact]
    public void Deep_nesting_doesnt_parse()
    {
        Assert.False(Filter.TryParse(new string('(', 200) + "qty eq 1" + new string(')', 200), out _));
        Assert.False(Filter.TryParse(string.Concat(Enumerable.Repeat("not ", 200)) + "qty eq 1", out _));
    }

    [Theory]
    [InlineData("name eq 'x'' or 1 eq 1 --'", new int[0])]
    [InlineData("name eq ') or (qty gt 0'", new int[0])]
    [InlineData("name contains '%'", new int[0])]
    [InlineData("name contains '_'", new int[0])]
    [InlineData("email contains '@'", new[] { 1, 4 })]
    public void Text_that_looks_like_code_stays_a_value(string text, int[] expected)
    {
        var filter = Filter.Parse(text);

        Assert.Equal(expected, InMemory(filter));
        Assert.Equal(expected, InSqlite(filter));
    }

    [Fact]
    public void Parsed_filters_with_unknown_fields_or_bad_values_match_nothing()
    {
        Assert.Empty(InMemory(Filter.Parse("salary gt 0")));
        Assert.Empty(InMemory(Filter.Parse("qty eq 'many'")));
        Assert.Equal(new[] { 1, 2, 3, 4 }, InMemory(Filter.Parse("not (salary gt 0)")));
    }

    [Fact]
    public void Fields_come_from_the_type_with_its_annotations()
    {
        var keys = Fields.Select(f => f.Key).ToArray();
        Assert.Equal(new[] { "id", "name", "email", "qty", "score", "rank", "tier", "active", "joined", "birthday", "left", "key" }, keys);

        var name = Fields.Single(f => f.Key == "name");
        Assert.Equal("Full name", name.Caption);
        Assert.True(name.Searchable && name.Sortable && name.Filterable);
        Assert.Equal(typeof(string), name.Type);
        Assert.Equal("N1", Fields.Single(f => f.Key == "score").Format);
        Assert.Equal("d", Fields.Single(f => f.Key == "joined").Format);
        Assert.Equal("Birthday", Fields.Single(f => f.Key == "birthday").Caption);
        Assert.False(Fields.Single(f => f.Key == "qty").Searchable);
        Assert.Equal(new[] { ("Basic", "Basic"), ("Gold", "Gold member"), ("Platinum", "Platinum") }, Fields.Single(f => f.Key == "tier").Choices);
        Assert.Same(Fields, DataFields.Of<Member>());
    }

    [Fact]
    public void A_field_narrows_its_operators()
    {
        var fields = new[] { new DataField<Member> { Key = "qty", Value = (Expression<Func<Member, int>>)(m => m.Qty), Filterable = true, Operators = [FilterOperator.Equals] } };
        var request = new DataRequest<Member> { Filter = Filter.Where("qty", FilterOperator.GreaterThan, 1) };

        Assert.Empty(request.Apply(Members.AsQueryable(), fields));
        Assert.Single((request with { Filter = Filter.Where("qty", FilterOperator.Equals, 1) }).Apply(Members.AsQueryable(), fields));
    }

    [Fact]
    public void A_custom_condition_replaces_the_default_and_null_falls_back()
    {
        FieldFilter? seen = null;
        var fields = new[]
        {
            new DataField<Member>
            {
                Key = "domain",
                Value = (Expression<Func<Member, string?>>)(m => m.Email),
                Filterable = true,
                // "domain eq example.com": the address ends with "@example.com"; anything else as usual.
                Filter = f =>
                {
                    seen = f;
                    return f.Operator == FilterOperator.Equals && f.Value is string domain ? m => m.Email != null && m.Email.EndsWith("@" + domain) : null;
                },
            },
            new DataField<Member>
            {
                Key = "qty",
                Value = (Expression<Func<Member, int>>)(m => m.Qty),
                Filterable = true,
                Filter = f => f.Operator == FilterOperator.Between ? m => m.Qty >= (int)f.Value! * 10 : null,
            },
        };

        int[] Ids(Filter filter, IQueryable<Member> source) =>
            new DataRequest<Member> { Filter = filter }.Apply(source, fields).Select(m => m.Id).ToList().OrderBy(i => i).ToArray();

        Assert.Equal(new[] { 1, 4 }, Ids(Filter.Where("domain", FilterOperator.Equals, "example.com"), Members.AsQueryable()));
        Assert.Equal(new[] { 1, 4 }, Ids(Filter.Where("domain", FilterOperator.Equals, "example.com"), _db.Members.AsNoTracking()));
        Assert.Equal(new[] { 1 }, Ids(Filter.Where("domain", FilterOperator.Contains, "ada"), Members.AsQueryable()));
        Assert.Equal(FilterOperator.Contains, seen!.Operator);

        // The values arrive as the field's type.
        Assert.Equal(new[] { 4 }, Ids(Filter.Between("qty", "1", null), Members.AsQueryable()));
    }

    [Fact]
    public void A_field_value_must_take_the_item()
    {
        Assert.Throws<ArgumentException>(() => new DataField<Member> { Key = "x", Value = (Expression<Func<string, int>>)(s => s.Length) });
    }

    [Fact]
    public void A_filter_too_wide_or_deep_matches_nothing_instead_of_overflowing()
    {
        var wide = Filter.Any(Enumerable.Range(0, 20_000).Select(i => (Filter)Filter.Where("name", FilterOperator.Equals, $"n{i}")).Append(Filter.Where("qty", FilterOperator.Equals, 2)).ToArray());
        Filter deep = Filter.Where("qty", FilterOperator.Equals, 2);
        for (var i = 0; i < 10_000; i++)
        {
            deep = Filter.Not(Filter.Not(deep));
        }

        Assert.Empty(InMemory(wide));
        Assert.Empty(InMemory(deep));

        // Within the limits, a wide or is a balanced tree, and works.
        var fine = Filter.Any(Enumerable.Range(0, 900).Select(i => (Filter)Filter.Where("name", FilterOperator.Equals, $"n{i}")).Append(Filter.Where("qty", FilterOperator.Equals, 2)).ToArray());
        Assert.Equal(new[] { 1 }, InMemory(fine));
        Assert.Equal(new[] { 1 }, InSqlite(fine));
    }

    [Fact]
    public void Large_whole_numbers_keep_every_digit()
    {
        var rows = new[] { (Id: 1, Big: 9007199254740992L), (Id: 2, Big: 9007199254740993L) };
        var fields = new[] { Field<(int Id, long Big), long>("big", r => r.Big) };
        int[] Ids(Filter f) => new DataRequest<(int Id, long Big)> { Filter = f }.Apply(rows.AsQueryable(), fields).Select(r => r.Id).ToArray();

        Assert.Equal(new[] { 2 }, Ids(Filter.Where("big", FilterOperator.Equals, "9007199254740993")));
        Assert.Equal(new[] { 2 }, Ids(Filter.Between("big", "9007199254740993", null)));
    }

    [Fact]
    public void A_local_date_time_compares_with_a_date_time_offset()
    {
        var at = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);
        var rows = new[] { (Id: 1, At: at) };
        var fields = new[] { Field<(int Id, DateTimeOffset At), DateTimeOffset>("at", r => r.At) };
        var request = new DataRequest<(int Id, DateTimeOffset At)> { Filter = Filter.Where("at", FilterOperator.LessThan, at.AddHours(1).LocalDateTime) };

        Assert.Single(request.Apply(rows.AsQueryable(), fields));
    }

    [Fact]
    public void A_field_narrows_its_operators_but_cant_widen_them()
    {
        var fields = new[] { new DataField<Member> { Key = "qty", Value = (Expression<Func<Member, int>>)(m => m.Qty), Filterable = true, Operators = [FilterOperator.IsEmpty, FilterOperator.Contains, FilterOperator.Equals] } };

        Assert.Empty(new DataRequest<Member> { Filter = Filter.Where("qty", FilterOperator.IsEmpty) }.Apply(Members.AsQueryable(), fields));
        Assert.Empty(new DataRequest<Member> { Filter = Filter.Where("qty", FilterOperator.Contains, "1") }.Apply(Members.AsQueryable(), fields));
        Assert.Single(new DataRequest<Member> { Filter = Filter.Where("qty", FilterOperator.Equals, 1) }.Apply(Members.AsQueryable(), fields));
    }

    [Fact]
    public void OData_after_json_writes_values_as_their_fields_types()
    {
        var request = new DataRequest<Member>
        {
            Filter = Filter.All(
                Filter.Where("key", FilterOperator.Equals, Guid.Parse("11111111-1111-1111-1111-111111111111")),
                Filter.Where("joined", FilterOperator.GreaterThanOrEqual, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
                Filter.Where("tier", FilterOperator.Equals, Tier.Gold)),
        };
        var back = JsonSerializer.Deserialize<DataRequest<Member>>(JsonSerializer.Serialize(request))!;

        Assert.Contains("key eq 11111111-1111-1111-1111-111111111111 and joined ge 2026-01-02T00:00:00Z and tier eq 'Gold'",
            Uri.UnescapeDataString(back.ToODataQuery(Fields)), StringComparison.Ordinal);
    }

    private static DataField<T> Field<T, TValue>(string key, Expression<Func<T, TValue>> value) =>
        new() { Key = key, Value = value, Sortable = true, Filterable = true };
}
