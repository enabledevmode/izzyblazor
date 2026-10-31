using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IzzyBlazor.Tests;

// The values a column's filter can list: through these fields only, in memory and through SQLite.
public sealed class FilterValuesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MemberDb _db;

    private static readonly Member[] Rows =
    [
        new() { Id = 1, Name = "Ada", Email = "ada@x.org", Qty = 1, Tier = Tier.Gold },
        new() { Id = 2, Name = "Grace", Email = "grace@x.org", Qty = 2, Tier = Tier.Basic },
        new() { Id = 3, Name = "Alan", Email = null, Qty = 3, Tier = Tier.Basic },
        new() { Id = 4, Name = "Adele", Email = "", Qty = 4, Tier = Tier.Gold },
        new() { Id = 5, Name = "Ada", Email = "ada2@x.org", Qty = 5, Tier = Tier.Platinum },
    ];

    private static readonly IReadOnlyList<DataField<Member>> Fields = DataFields.Of<Member>();

    public FilterValuesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new MemberDb(new DbContextOptionsBuilder<MemberDb>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.Members.AddRange(Rows.Select(r => new Member { Id = r.Id, Name = r.Name, Email = r.Email, Qty = r.Qty, Tier = r.Tier }));
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static string?[] Values(FilterValuesResult result) => result.Values.Select(v => v.Value).ToArray();

    // Both ways come to the same list.
    private FilterValuesResult Both(FilterValuesRequest request)
    {
        var memory = request.ToResult(Rows.AsQueryable(), Fields);
        var database = request.ToResultAsync(_db.Members.AsNoTracking(), Fields, (q, ct) => q.ToListAsync(ct)).AsTask().GetAwaiter().GetResult();
        Assert.Equal(Values(memory).OrderBy(v => v, StringComparer.Ordinal), Values(database).OrderBy(v => v, StringComparer.Ordinal));
        Assert.Equal(memory.HasMore, database.HasMore);
        return memory;
    }

    [Fact]
    public void Lists_each_value_once_sorted()
    {
        Assert.Equal(new[] { "Ada", "Adele", "Alan", "Grace" }, Values(Both(new() { Field = "name" })));
    }

    [Fact]
    public void Empty_text_and_no_text_are_one_blank_row()
    {
        Assert.Single(Values(Both(new() { Field = "email" })), v => v is null);
    }

    [Fact]
    public void A_search_narrows_ignoring_case_by_default()
    {
        Assert.Equal(new[] { "Ada", "Adele" }, Values(Both(new() { Field = "name", Search = " aD " })));
    }

    [Fact]
    public void A_search_follows_the_fields_case()
    {
        DataField<Member>[] fields = [new() { Key = "name", Value = (System.Linq.Expressions.Expression<Func<Member, string>>)(m => m.Name), Filterable = true, Case = FilterCase.MatchCase }];

        Assert.Empty(new FilterValuesRequest { Field = "name", Search = "ad" }.ToResult(Rows.AsQueryable(), fields).Values);
    }

    [Fact]
    public void The_other_filters_and_the_grids_search_narrow_it()
    {
        Assert.Equal(new[] { "Ada", "Adele" }, Values(Both(new() { Field = "name", OtherFilters = Filter.Where("tier", FilterOperator.Equals, "Gold") })));
        Assert.Equal(new[] { "Grace" }, Values(Both(new() { Field = "name", GridSearch = "gra" })));
    }

    [Fact]
    public void Whats_ticked_or_unticked_stays_listed_first_whatever_the_others_leave()
    {
        var result = Both(new()
        {
            Field = "name",
            OtherFilters = Filter.Where("tier", FilterOperator.Equals, "Gold"),
            Ticked = ["Grace"],
            Unticked = ["Alan"],
        });

        Assert.Equal(new[] { "Grace", "Alan", "Ada", "Adele" }, Values(result));
    }

    [Fact]
    public void A_search_hides_whats_ticked_if_it_doesnt_match()
    {
        Assert.Equal(new[] { "Ada", "Adele" }, Values(Both(new() { Field = "name", Search = "ad", Ticked = ["Grace", null] })));
    }

    [Fact]
    public void A_ticked_blank_is_listed_once()
    {
        var values = Values(Both(new() { Field = "email", Ticked = [null] }));

        Assert.Null(values[0]);
        Assert.Single(values, v => v is null);
    }

    [Fact]
    public void Pages_say_if_theres_more_and_repeat_nothing()
    {
        var first = Both(new() { Field = "name", Take = 2, Ticked = ["Grace"] });
        Assert.Equal(new[] { "Grace", "Ada" }, Values(first));
        Assert.True(first.HasMore);

        var second = Both(new() { Field = "name", Skip = 2, Take = 2, Ticked = ["Grace"] });
        Assert.Equal(new[] { "Adele", "Alan" }, Values(second));
        Assert.False(second.HasMore);
    }

    [Fact]
    public void A_skip_inside_whats_kept_repeats_nothing()
    {
        Assert.Equal(new[] { "Grace", "Ada" }, Values(Both(new() { Field = "name", Skip = 1, Take = 2, Ticked = ["Alan", "Grace"] })));
    }

    [Fact]
    public void Take_has_a_limit()
    {
        var many = Enumerable.Range(0, 600).Select(i => new Member { Id = i, Name = $"n{i:000}" }).ToArray();
        var result = new FilterValuesRequest { Field = "name", Take = 100000 }.ToResult(many.AsQueryable(), Fields);

        Assert.Equal(500, result.Values.Count);
        Assert.True(result.HasMore);
    }

    [Fact]
    public void A_field_that_isnt_filterable_or_isnt_there_gives_nothing()
    {
        DataField<Member>[] fields = [new() { Key = "name", Value = (System.Linq.Expressions.Expression<Func<Member, string>>)(m => m.Name), Filterable = false }];

        Assert.Empty(new FilterValuesRequest { Field = "name" }.ToResult(Rows.AsQueryable(), fields).Values);
        Assert.Empty(new FilterValuesRequest { Field = "password" }.ToResult(Rows.AsQueryable(), Fields).Values);
    }

    [Fact]
    public void A_field_that_isnt_text_says_it_needs_a_loader()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new FilterValuesRequest { Field = "qty" }.ToResult(Rows.AsQueryable(), Fields));

        Assert.Contains(nameof(FilterValuesLoader), ex.Message);
    }

    [Fact]
    public void A_request_round_trips_through_its_query_string()
    {
        var request = new FilterValuesRequest
        {
            Field = "customer",
            Search = "a&b",
            Skip = 50,
            Take = 20,
            OtherFilters = Filter.Where("total", FilterOperator.GreaterThan, 100),
            GridSearch = "x y",
            Ticked = ["Smith, John", null],
            Unticked = ["Ada"],
        };

        var read = FilterValuesRequest.Parse("/orders/values?" + request.ToQueryString());

        Assert.Equal("customer", read.Field);
        Assert.Equal("a&b", read.Search);
        Assert.Equal(50, read.Skip);
        Assert.Equal(20, read.Take);
        Assert.Equal(request.OtherFilters!.ToString(), read.OtherFilters!.ToString());
        Assert.Equal("x y", read.GridSearch);
        Assert.Equal(new[] { "Smith, John", null }, read.Ticked);
        Assert.Equal(new[] { "Ada" }, read.Unticked);
    }

    [Fact]
    public void A_short_query_reads_as_the_defaults_and_a_bad_filter_matches_nothing()
    {
        var read = FilterValuesRequest.Parse("field=name&take=many&filter=%28%28");

        Assert.Equal(50, read.Take);
        Assert.Empty(new FilterValuesRequest { Field = "name", OtherFilters = read.OtherFilters }.ToResult(Rows.AsQueryable(), Fields).Values);
    }

    [Fact]
    public void A_result_round_trips_through_json()
    {
        var json = JsonSerializer.Serialize(new FilterValuesResult([new("1", "Ada"), new(null)], HasMore: true));

        var read = JsonSerializer.Deserialize<FilterValuesResult>(json)!;

        Assert.True(read.HasMore);
        Assert.Equal("Ada", read.Values[0].Text);
        Assert.Null(read.Values[1].Value);
    }

    private static DataField<Member> Loaded(FilterValuesLoader loader, bool filterable = true) => new()
    {
        Key = "name",
        Value = (System.Linq.Expressions.Expression<Func<Member, string>>)(m => m.Name),
        Filterable = filterable,
        FilterValues = loader,
    };

    [Fact]
    public async Task LoadValues_runs_the_fields_loader_and_adds_what_it_left_out()
    {
        DataField<Member>[] fields = [Loaded(_ => new(new FilterValuesResult([new("1", "Ada")], HasMore: true)))];

        var result = await DataFields.LoadValuesAsync(fields, new FilterValuesRequest { Field = "NAME", Ticked = ["7", "1"] });

        Assert.Equal(new[] { "7", "1" }, Values(result));
        Assert.Equal("Ada", result.Values[1].Text);
        Assert.True(result.HasMore);
    }

    [Fact]
    public async Task LoadValues_adds_nothing_to_a_later_page_or_what_a_search_hides()
    {
        DataField<Member>[] fields = [Loaded(_ => new(new FilterValuesResult([new("Ada")])))];

        Assert.Equal(new[] { "Ada" }, Values(await DataFields.LoadValuesAsync(fields, new FilterValuesRequest { Field = "name", Skip = 50, Ticked = ["Zed"] })));
        Assert.Equal(new[] { "Ada" }, Values(await DataFields.LoadValuesAsync(fields, new FilterValuesRequest { Field = "name", Search = "ad", Ticked = ["Zed"] })));
    }

    [Fact]
    public async Task LoadValues_gives_nothing_without_a_loader_or_a_filterable_field()
    {
        var called = false;
        DataField<Member>[] hidden = [Loaded(_ =>
        {
            called = true;
            return new(new FilterValuesResult([new("secret")]));
        }, filterable: false)];

        Assert.Empty((await DataFields.LoadValuesAsync(hidden, new FilterValuesRequest { Field = "name" })).Values);
        Assert.False(called);
        Assert.Empty((await DataFields.LoadValuesAsync(Fields, new FilterValuesRequest { Field = "name" })).Values);
        Assert.Empty((await DataFields.LoadValuesAsync(Fields, new FilterValuesRequest { Field = "password" })).Values);
    }
}
