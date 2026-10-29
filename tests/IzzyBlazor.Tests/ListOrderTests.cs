using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// The posted order of a SortableList and the move rules SortableList and DropZoneGroup share.
public sealed class ListOrderTests
{
    private sealed record Step(string Id, string? Zone = null);

    private static readonly Step[] Steps = [new("a"), new("b"), new("c"), new("d")];

    private static string Ids(IEnumerable<Step> steps) => string.Concat(steps.Select(s => s.Id));

    private static string Ids<T>(IEnumerable<ListEntry<T>> entries) => string.Join(" ", entries.Select(e => e.Zone is null ? e.Key : $"{e.Zone}:{e.Key}"));

    private static IReadOnlyList<Step> Arrange(string[]? keys, string? move = null) =>
        new ListOrder { Keys = keys, Move = move }.Arrange(Steps, s => s.Id);

    [Fact]
    public void Nothing_posted_keeps_the_order()
    {
        Assert.Equal("abcd", Ids(Arrange(null)));
        Assert.Equal("abcd", Ids(Arrange([])));
    }

    [Fact]
    public void Posted_keys_set_the_order()
    {
        Assert.Equal("dbca", Ids(Arrange(["d", "b", "c", "a"])));
    }

    [Fact]
    public void Unknown_and_repeated_keys_are_skipped()
    {
        Assert.Equal("cabd", Ids(Arrange(["c", "x", "a", "c", "", "b", "d"])));
    }

    [Fact]
    public void Items_the_post_left_out_stay_after_the_item_they_followed()
    {
        // c followed b, and d followed c, so they go after b in their own order.
        Assert.Equal("abcd", Ids(Arrange(["a", "b"])));
        Assert.Equal("bcda", Ids(Arrange(["b", "a"])));
        // a had nothing before it, so it goes first.
        Assert.Equal("adcb", Ids(Arrange(["d", "c", "b"])));
    }

    [Theory]
    [InlineData("up:c", "acbd")]
    [InlineData("down:b", "acbd")]
    [InlineData("top:d", "dabc")]
    [InlineData("bottom:a", "bcda")]
    [InlineData("up:a", "abcd")]
    [InlineData("down:d", "abcd")]
    [InlineData("top:a", "abcd")]
    [InlineData("up:x", "abcd")]
    [InlineData("sideways:b", "abcd")]
    [InlineData("up:", "abcd")]
    [InlineData(":b", "abcd")]
    [InlineData("to:done:b", "abcd")]
    public void Menu_moves_apply_after_the_posted_order(string move, string expected)
    {
        Assert.Equal(expected, Ids(Arrange(["a", "b", "c", "d"], move)));
    }

    [Fact]
    public void A_move_applies_to_the_posted_order_not_the_old_one()
    {
        // Script moved d to the top; then the menu moved b up, past a.
        Assert.Equal("dbac", Ids(Arrange(["d", "a", "b", "c"], "up:b")));
    }

    [Fact]
    public void Keys_may_hold_colons()
    {
        var items = new[] { new Step("x:1"), new Step("x:2") };
        Assert.Equal(["x:2", "x:1"], new ListOrder { Move = "up:x:2" }.Arrange(items, s => s.Id).Select(s => s.Id));
    }

    [Theory]
    [InlineData("up:a", "Up", "a", null)]
    [InlineData("bottom:a:b", "Bottom", "a:b", null)]
    [InlineData("to:done:a:b", "To", "a:b", "done")]
    public void Moves_parse_and_print(string text, string kind, string key, string? zone)
    {
        Assert.True(ListMove.TryParse(text, out var move));
        Assert.Equal(new ListMove(Enum.Parse<ListMoveKind>(kind), key, zone), move);
        Assert.Equal(text, move.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("up")]
    [InlineData("to:done")]
    [InlineData("to::a")]
    [InlineData("to:done:")]
    [InlineData("UP:a")]
    public void Bad_moves_dont_parse(string? text)
    {
        Assert.False(ListMove.TryParse(text, out _));
    }

    // Zones: one list, each item in a zone; moves within a zone pass items of other zones.

    private static readonly Step[] Board = [new("a", "todo"), new("b", "doing"), new("c", "todo"), new("d", "done"), new("e", "todo")];

    private static List<ListEntry<Step>> Entries(string[]? keys = null, string[]? zones = null) =>
        ListArrangement.Order(Board, s => s.Id, keys, zones, s => s.Zone);

    [Fact]
    public void Zones_come_from_the_item_unless_posted()
    {
        Assert.Equal("todo:a doing:b todo:c done:d todo:e", Ids(Entries()));
        // d and e weren't posted: they follow c, as before, with their own zones.
        Assert.Equal("done:c done:d todo:e todo:a doing:b", Ids(Entries(["c", "a", "b"], ["done", "todo", ""])));
    }

    [Fact]
    public void Up_and_down_move_within_the_zone()
    {
        var entries = Entries();
        var moved = ListArrangement.Apply(entries, new ListMove(ListMoveKind.Up, "c"));
        Assert.Equal("todo:c todo:a doing:b done:d todo:e", Ids(entries));
        Assert.Equal(new ListMoved("c", "todo", 1, "todo", 0), moved);

        entries = Entries();
        moved = ListArrangement.Apply(entries, new ListMove(ListMoveKind.Down, "c"));
        Assert.Equal("todo:a doing:b done:d todo:e todo:c", Ids(entries));
        Assert.Equal(new ListMoved("c", "todo", 1, "todo", 2), moved);
    }

    [Fact]
    public void Ends_of_a_zone_go_nowhere()
    {
        var entries = Entries();
        Assert.Null(ListArrangement.Apply(entries, new ListMove(ListMoveKind.Up, "a")));
        Assert.Null(ListArrangement.Apply(entries, new ListMove(ListMoveKind.Down, "e")));
        Assert.Null(ListArrangement.Apply(entries, new ListMove(ListMoveKind.Bottom, "e")));
        Assert.Null(ListArrangement.Apply(entries, new ListMove(ListMoveKind.To, "b", "doing")));
        Assert.Equal("todo:a doing:b todo:c done:d todo:e", Ids(entries));
    }

    [Fact]
    public void Moving_to_a_zone_goes_to_its_end()
    {
        var entries = Entries();
        var moved = ListArrangement.Apply(entries, new ListMove(ListMoveKind.To, "a", "done"));
        Assert.Equal("doing:b todo:c done:d done:a todo:e", Ids(entries));
        Assert.Equal(new ListMoved("a", "todo", 0, "done", 1), moved);
    }

    [Fact]
    public void Moving_to_an_empty_zone_goes_to_the_end_of_the_list()
    {
        var entries = Entries();
        var moved = ListArrangement.Apply(entries, new ListMove(ListMoveKind.To, "c", "later"));
        Assert.Equal("todo:a doing:b done:d todo:e later:c", Ids(entries));
        Assert.Equal(new ListMoved("c", "todo", 1, "later", 0), moved);
    }

    [Theory]
    [InlineData(0, "todo:e todo:a doing:b todo:c done:d")]
    [InlineData(1, "todo:a doing:b todo:e todo:c done:d")]
    [InlineData(-5, "todo:e todo:a doing:b todo:c done:d")]
    [InlineData(99, "todo:a doing:b todo:c done:d todo:e")]
    public void A_drop_goes_to_a_place_in_the_zone_clamped_to_its_ends(int index, string expected)
    {
        var entries = Entries();
        ListArrangement.MoveTo(entries, "e", "todo", index);
        Assert.Equal(expected, Ids(entries));
    }

    [Fact]
    public void A_drop_into_another_zone_at_a_place()
    {
        var entries = Entries();
        var moved = ListArrangement.MoveTo(entries, "e", "doing", 0);
        Assert.Equal("todo:a doing:e doing:b todo:c done:d", Ids(entries));
        Assert.Equal(new ListMoved("e", "todo", 2, "doing", 0), moved);
    }

    // Binding: the names a list posts bind to ListOrder with [SupplyParameterFromForm] on a static
    // Blazor page (ListOrderFormProbe.razor). Minimal APIs map the same type without the prefix.

    [Fact]
    public async Task Posted_fields_bind_to_a_ListOrder()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents();
        await using var app = builder.Build();
        app.UseDeveloperExceptionPage();
        app.UseAntiforgery();
        app.MapRazorComponents<ListOrderFormProbe>();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        var form = new FormUrlEncodedContent(
        [
            new("_handler", "probe"),
            new("order.Keys", "c"), new("order.Zones", "done"),
            new("order.Keys", "a:1"), new("order.Zones", "todo"),
            new("order.Keys", "b"), new("order.Zones", "todo"),
            new("order.Move", "up:a:1"),
        ]);
        var response = await client.PostAsync("/list-order-probe", form);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains("<p id=\"out\">c,a:1,b|done,todo,todo|up:a:1</p>", body);
    }
}
