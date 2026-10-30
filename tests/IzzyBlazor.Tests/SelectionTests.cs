using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// The selection model: keys, or all matching a filter except keys; applying it to a query
// (in memory and through EF Core), and binding it from a static page's post.
public sealed class SelectionTests : IDisposable
{
    private static readonly Member[] Members =
    [
        new() { Id = 1, Name = "Ada", Qty = 2, Tier = Tier.Gold },
        new() { Id = 2, Name = "Grace", Qty = 3, Tier = Tier.Basic },
        new() { Id = 3, Name = "adam", Qty = 1, Tier = Tier.Platinum },
        new() { Id = 4, Name = "Alan", Qty = 3, Tier = Tier.Gold },
        new() { Id = 5, Name = "Katherine", Qty = 5, Tier = Tier.Basic },
    ];

    private readonly SqliteConnection _connection;
    private readonly MemberDb _db;

    public SelectionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new MemberDb(new DbContextOptionsBuilder<MemberDb>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.Members.AddRange(Members.Select(m => new Member { Id = m.Id, Name = m.Name, Qty = m.Qty, Tier = m.Tier }));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static int[] Ids(IEnumerable<Member> members) => [.. members.Select(m => m.Id).Order()];

    private int[] Both(Selection selection)
    {
        var memory = Ids(selection.Apply(Members.AsQueryable(), m => m.Id.ToString()));
        var database = Ids(selection.Apply(_db.Members.AsNoTracking(), m => m.Id.ToString()).ToList());
        Assert.Equal(memory, database);
        return memory;
    }

    [Fact]
    public void Keys_select_those_items()
    {
        Assert.Equal(new[] { 2, 4 }, Both(Selection.Of("4", "2", "4", "99")));
    }

    [Fact]
    public void Nothing_selected_applies_to_nothing()
    {
        Assert.Empty(Both(Selection.None));
        Assert.True(Selection.None.IsEmpty);
    }

    [Fact]
    public void All_selects_what_the_filter_and_search_match_except_the_keys()
    {
        var all = Selection.AllOf(new DataRequest<Member> { Filter = Filter.Where("qty", FilterOperator.GreaterThanOrEqual, 2) });

        Assert.Equal(new[] { 1, 2, 4, 5 }, Both(all));
        Assert.Equal(new[] { 1, 4, 5 }, Both(all.With("2", selected: false)));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Both(new Selection { All = true }));
        Assert.Equal(new[] { 1, 3, 4 }, Both(new Selection { All = true, Search = "a", Keys = ["2", "5"] }));
    }

    [Fact]
    public void A_filter_it_cannot_read_selects_nothing()
    {
        Assert.Empty(Both(new Selection { All = true, Filter = "qty gt" }));
    }

    [Fact]
    public void A_filter_reaches_only_the_given_fields()
    {
        // Secret is ScaffoldColumn(false): not a field, so a posted filter on it matches nothing.
        Assert.Empty(Both(new Selection { All = true, Filter = "secret eq 's1'" }));
    }

    [Fact]
    public void With_adds_and_removes_keys_and_keeps_the_scope()
    {
        var some = Selection.None.With("a", true).With("b", true).With("a", false);
        Assert.Equal(new[] { "b" }, some.Keys);
        Assert.Same(some, some.With("b", true));

        var all = new Selection { All = true, Filter = "qty gt 1" }.With("x", false);
        Assert.False(all.Contains("x"));
        Assert.True(all.Contains("y"));
        Assert.Equal("qty gt 1", all.Filter);
        Assert.True(all.With("x", true).Contains("x"));
        Assert.Empty(all.With("x", true).Keys!);
    }

    [Fact]
    public void Count_knows_the_total_only_under_All()
    {
        Assert.Equal(2, Selection.Of("a", "b").Count());
        Assert.Null(new Selection { All = true, Keys = ["a"] }.Count());
        Assert.Equal(9, new Selection { All = true, Keys = ["a"] }.Count(10));
    }

    [Fact]
    public void MatchesScope_compares_the_filter_and_search_All_was_made_under()
    {
        var filter = Filter.Where("qty", FilterOperator.GreaterThan, 1);
        var all = Selection.AllOf(new DataRequest<Member> { Filter = filter, Search = "a" });

        Assert.True(all.MatchesScope(filter, "a"));
        Assert.False(all.MatchesScope(filter, null));
        Assert.False(all.MatchesScope(null, "a"));
        Assert.True(Selection.Of("1").MatchesScope(null, "anything"));
    }

    [Fact]
    public async Task Posted_fields_bind_to_a_Selection()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents();
        await using var app = builder.Build();
        app.UseDeveloperExceptionPage();
        app.UseAntiforgery();
        app.MapRazorComponents<SelectionFormProbe>();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        var form = new FormUrlEncodedContent(
        [
            new("_handler", "probe"),
            new("selected.Keys", "a:1"), new("selected.Keys", "b"),
            new("selected.All", "true"),
            new("selected.Filter", "qty gt 1"),
            new("selected.Search", "ada"),
        ]);
        var response = await client.PostAsync("/selection-probe", form);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains("<p id=\"out\">a:1,b|True|qty gt 1|ada</p>", body);
    }
}
