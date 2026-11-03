using System.Linq.Expressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IzzyBlazor.Tests;

// The group query: the same request gives the same groups from a list and from a database.
public sealed class DataGroupTests : IDisposable
{
    private static readonly Sale[] Sales =
    [
        new() { Id = 1, Region = "East", Rep = "Ada", Units = 2, Price = 10m, Day = new(2025, 1, 5) },
        new() { Id = 2, Region = "East", Rep = "Ada", Units = 4, Price = 20m, Day = new(2026, 1, 6) },
        new() { Id = 3, Region = "East", Rep = null, Units = 1, Price = 30m, Day = new(2026, 2, 7) },
        new() { Id = 4, Region = "West", Rep = "Bo", Units = 3, Price = 40m, Day = new(2026, 2, 8) },
        new() { Id = 5, Region = "West", Rep = "Bo", Units = 5, Price = 50m, Day = new(2026, 3, 9) },
        new() { Id = 6, Region = "North", Rep = "Cy", Units = 6, Price = 60m, Day = new(2026, 3, 10) },
    ];

    private static readonly IReadOnlyList<DataField<Sale>> Fields =
    [
        .. DataFields.Of<Sale>(),
        new()
        {
            Key = "year",
            Value = (Expression<Func<Sale, int>>)(s => s.Day.Year),
            Groupable = true,
        },
        new()
        {
            Key = "weighted",
            Value = (Expression<Func<Sale, int>>)(s => s.Units),
            Aggregatable = true,
            AggregateBy = (Expression<Func<IEnumerable<Sale>, double>>)(g => g.Sum(s => s.Units * s.Scale) / g.Sum(s => s.Scale)),
        },
    ];

    private readonly SqliteConnection _connection;
    private readonly SaleDb _db;

    public DataGroupTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new SaleDb(new DbContextOptionsBuilder<SaleDb>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.Sales.AddRange(Sales.Select(s => new Sale { Id = s.Id, Region = s.Region, Rep = s.Rep, Units = s.Units, Price = s.Price, Day = s.Day }));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private (DataGroupResult List, DataGroupResult Database) Both(DataGroupRequest<Sale> request) =>
        (request.ToResult(Sales.AsQueryable(), Fields), request.ToResult(_db.Sales.AsNoTracking(), Fields));

    private static string Text(DataGroup group) =>
        string.Join("/", group.Keys.Select(k => k ?? "null")) + ":" + group.Count;

    [Fact]
    public void Two_levels_ordered_and_paged_with_a_count_of_all_the_groups()
    {
        var request = new DataGroupRequest<Sale> { Keys = [new("region", Descending: true), new("rep")], Skip = 1, Take = 2, NeedsTotal = true };

        foreach (var result in new[] { Both(request).List, Both(request).Database })
        {
            Assert.Equal(new[] { "North/Cy:1", "East/null:1" }, result.Groups.Select(Text));
            Assert.Equal(4, result.Total);
        }
    }

    [Fact]
    public void Aggregates_cover_the_whole_group_and_what_the_filter_keeps()
    {
        var request = new DataGroupRequest<Sale>
        {
            Keys = [new("region")],
            Take = 1,
            Filter = Filter.Where("units", FilterOperator.GreaterThan, 1),
            Aggregates = [new("units", DataAggregate.Sum), new("price", DataAggregate.Avg), new("day", DataAggregate.Max), new("rep", DataAggregate.Min), new("id", DataAggregate.Count), new("weighted", DataAggregate.Custom)],
        };

        foreach (var east in new[] { Both(request).List.Groups.Single(), Both(request).Database.Groups.Single() })
        {
            Assert.Equal(2, east.Count);
            Assert.Equal(6, Convert.ToInt32(east.Values["units:sum"]));
            Assert.Equal(15m, Convert.ToDecimal(east.Values["price:avg"]));
            Assert.Equal(new DateOnly(2026, 1, 6), east.Values["day:max"]);
            Assert.Equal("Ada", east.Values["rep:min"]);
            Assert.Equal(2, Convert.ToInt32(east.Values["id:count"]));
            Assert.Equal(3d, Convert.ToDouble(east.Values["weighted:custom"]));
        }
    }

    [Fact]
    public void No_keys_is_the_grand_total_even_of_nothing()
    {
        var all = new DataGroupRequest<Sale> { Aggregates = [new("units", DataAggregate.Sum)], Skip = 3, Take = 1 };
        var none = all with { Filter = Filter.Where("units", FilterOperator.GreaterThan, 100) };

        foreach (var result in new[] { Both(all).List, Both(all).Database })
        {
            Assert.Equal(6, result.Groups.Single().Count);
            Assert.Equal(21, Convert.ToInt32(result.Groups.Single().Values["units:sum"]));
        }

        foreach (var result in new[] { Both(none).List, Both(none).Database })
        {
            Assert.Equal(0, result.Groups.Single().Count);
        }
    }

    [Fact]
    public void A_computed_field_groups_and_an_open_groups_filter_finds_its_rows()
    {
        var request = new DataGroupRequest<Sale> { Keys = [new("year")] };
        foreach (var result in new[] { Both(request).List, Both(request).Database })
        {
            Assert.Equal(new[] { "2025:1", "2026:5" }, result.Groups.Select(Text));
        }

        // A null key finds its own rows, as a value does.
        var open = new DataRequest<Sale> { Filter = Filter.All(new FieldFilter("region", FilterOperator.Equals, "East", MatchCase: true), new FieldFilter("rep", FilterOperator.Equals, null, MatchCase: true)) };
        Assert.Equal(3, open.Apply(Sales.AsQueryable(), Fields).Single().Id);
        Assert.Equal(3, open.Apply(_db.Sales.AsNoTracking(), Fields).Single().Id);
    }

    [Fact]
    public async Task Async_through_the_apps_own_methods()
    {
        var request = new DataGroupRequest<Sale> { Keys = [new("region"), new("rep")], Aggregates = [new("units", DataAggregate.Sum)], Take = 2, NeedsTotal = true };

        var result = await request.ToResultAsync(_db.Sales.AsNoTracking(), Fields, (q, ct) => q.CountAsync(ct), (q, ct) => q.ToListAsync(ct));

        Assert.Equal(4, result.Total);
        Assert.Equal(new[] { "East/null:1", "East/Ada:2" }, result.Groups.Select(Text));
    }

    [Fact]
    public void Over_the_wire_it_reads_back_and_reaches_only_the_fields_allowed()
    {
        var request = new DataGroupRequest<Sale>
        {
            Keys = [new("region", Descending: true)],
            Aggregates = [new("units", DataAggregate.Sum)],
            Filter = Filter.Where("units", FilterOperator.GreaterThan, 1),
            Search = "a",
            Skip = 2,
            Take = 3,
            NeedsTotal = true,
        };

        var parsed = DataGroupRequest<Sale>.Parse(request.ToQueryString());

        Assert.Equal(request.ToQueryString(), parsed.ToQueryString());

        // Not a field the server lists, and an aggregate its type can't take: left out.
        var forged = DataGroupRequest<Sale>.Parse("group=secret,region&aggregate=secret:sum,rep:sum,units:max");
        var allowed = DataFields.Of<Sale>().Where(f => f.Key != "secret").ToList();
        var group = forged.ToResult(Sales.AsQueryable(), allowed).Groups[0];
        Assert.Single(group.Keys);
        Assert.Equal("East", group.Keys[0]);
        Assert.Equal(["units:max"], group.Values.Keys);
    }

    public sealed class Sale
    {
        public int Id { get; set; }

        public string Region { get; set; } = "";

        public string? Rep { get; set; }

        public int Units { get; set; }

        public decimal Price { get; set; }

        public DateOnly Day { get; set; }

        public string Secret { get; set; } = "";

        public double Scale { get; set; } = 1;
    }

    private sealed class SaleDb(DbContextOptions<SaleDb> options) : DbContext(options)
    {
        public DbSet<Sale> Sales => Set<Sale>();
    }
}
