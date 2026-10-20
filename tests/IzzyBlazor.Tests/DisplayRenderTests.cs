using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class DisplayRenderTests : BunitContext
{
    public DisplayRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    [Theory]
    [InlineData("Ctrl+K", new[] { "Ctrl", "K" })]
    [InlineData("Ctrl + Shift + P", new[] { "Ctrl", "Shift", "P" })]
    [InlineData("Ctrl++", new[] { "Ctrl", "+" })]
    [InlineData("Ctrl + +", new[] { "Ctrl", "+" })]
    [InlineData("+", new[] { "+" })]
    [InlineData("Esc", new[] { "Esc" })]
    [InlineData("  ", new string[0])]
    public void Keys_split_on_plus(string keys, string[] expected) =>
        Assert.Equal(expected, Kbd.SplitKeys(keys));

    [Fact]
    public void Kbd_is_one_key_or_nested_keys()
    {
        var single = Render<Kbd>(p => p.AddChildContent("Esc")).Find("kbd");
        Assert.Equal("ib-kbd", single.ClassName);
        Assert.Equal("Esc", single.TextContent);

        var combo = Render<Kbd>(p => p.Add(k => k.Keys, "Ctrl+K")).Find("kbd.ib-kbd-combo");
        Assert.Equal(["Ctrl", "K"], combo.QuerySelectorAll("kbd.ib-kbd").Select(k => k.TextContent));
        Assert.Equal("+", combo.QuerySelector(".ib-kbd-separator")!.TextContent);
    }

    [Fact]
    public void DescriptionList_is_a_dl_of_wrapped_pairs()
    {
        var cut = Render<DescriptionList>(p => p
            .Add(d => d.Divided, true)
            .AddChildContent<DescriptionItem>(i => i.Add(x => x.Term, "Status").AddChildContent("Paid")));

        var dl = cut.Find("dl");
        Assert.Contains("ib-dl-horizontal", dl.ClassName);
        Assert.Contains("ib-dl-divided", dl.ClassName);
        var item = dl.QuerySelector(":scope > div.ib-dl-item")!;
        Assert.Equal("Status", item.QuerySelector("dt")!.TextContent.Trim());
        Assert.Equal("Paid", item.QuerySelector("dd")!.TextContent);
    }

    [Fact]
    public void Columns_apply_only_to_vertical_lists_and_are_clamped()
    {
        var vertical = Render<DescriptionList>(p => p.Add(d => d.Orientation, Orientation.Vertical).Add(d => d.Columns, 9)).Find("dl");
        Assert.Contains("ib-dl-columns", vertical.ClassName);
        Assert.Contains("--ib-dl-columns:4", vertical.GetAttribute("style"));

        var horizontal = Render<DescriptionList>(p => p.Add(d => d.Columns, 3)).Find("dl");
        Assert.DoesNotContain("ib-dl-columns", horizontal.ClassName);
    }

    [Fact]
    public void Stat_names_its_value_and_reads_the_trend_in_words()
    {
        var stat = Render<Stat>(p => p
            .Add(s => s.Label, "Revenue")
            .Add(s => s.Value, "$48,210")
            .Add(s => s.Change, "12%")
            .Add(s => s.Trend, StatTrend.Up)
            .Add(s => s.Description, "vs last month")).Find(".ib-stat");

        Assert.Equal("Revenue", stat.QuerySelector("dl > dt")!.TextContent);
        Assert.Equal("$48,210", stat.QuerySelector(".ib-stat-value")!.TextContent.Trim());
        var change = stat.QuerySelector(".ib-stat-change")!;
        Assert.Contains("ib-stat-change-success", change.ClassName);
        Assert.Equal("true", change.QuerySelector("svg")!.GetAttribute("aria-hidden"));
        Assert.Equal("Up ", change.QuerySelector(".ib-visually-hidden")!.TextContent);
        Assert.Equal("vs last month", stat.QuerySelector(".ib-stat-description")!.TextContent);
    }

    [Theory]
    [InlineData(StatTrend.Down, null, "danger", "Down ")]
    [InlineData(StatTrend.Down, Status.Success, "success", "Down ")]
    [InlineData(StatTrend.Flat, null, "neutral", "No change ")]
    public void Change_color_follows_the_trend_unless_set(StatTrend trend, Status? status, string color, string label)
    {
        var change = Render<Stat>(p => p
            .Add(s => s.Label, "Response time")
            .Add(s => s.Change, "24 ms")
            .Add(s => s.Trend, trend)
            .Add(s => s.ChangeStatus, status)).Find(".ib-stat-change");

        Assert.Contains($"ib-stat-change-{color}", change.ClassName);
        Assert.Equal(label, change.QuerySelector(".ib-visually-hidden")!.TextContent);
    }

    [Fact]
    public void Stat_without_a_trend_has_no_arrow_or_meta_when_empty()
    {
        var plain = Render<Stat>(p => p.Add(s => s.Label, "Orders").Add(s => s.Value, "128")).Find(".ib-stat");
        Assert.Null(plain.QuerySelector(".ib-stat-meta"));

        var change = Render<Stat>(p => p.Add(s => s.Label, "Orders").Add(s => s.Change, "+8")).Find(".ib-stat-change");
        Assert.Null(change.QuerySelector("svg"));
        Assert.Null(change.QuerySelector(".ib-visually-hidden"));
    }

    [Fact]
    public void EmptyState_renders_its_parts_with_the_title_level()
    {
        var empty = Render<EmptyState>(p => p
            .Add(e => e.Icon, Lucide.Inbox)
            .Add(e => e.Title, "No invoices yet")
            .Add(e => e.TitleLevel, 2)
            .Add(e => e.Description, "Invoices you send appear here.")
            .Add(e => e.Bordered, true)
            .AddChildContent(b =>
            {
                b.OpenComponent<EmptyStateActions>(0);
                b.AddAttribute(1, nameof(EmptyStateActions.ChildContent), (RenderFragment)(a => a.AddContent(0, "New invoice")));
                b.CloseComponent();
            })).Find(".ib-empty");

        Assert.Contains("ib-empty-bordered", empty.ClassName);
        Assert.Equal("true", empty.QuerySelector(".ib-empty-icon")!.GetAttribute("aria-hidden"));
        Assert.Equal("No invoices yet", empty.QuerySelector("h2.ib-empty-title")!.TextContent);
        Assert.Equal("Invoices you send appear here.", empty.QuerySelector(".ib-empty-description")!.TextContent);
        Assert.Equal("New invoice", empty.QuerySelector(".ib-empty-actions")!.TextContent);
    }

    [Fact]
    public void Blank_keys_fall_back_to_the_content()
    {
        var kbd = Render<Kbd>(p => p.Add(k => k.Keys, " ").AddChildContent("Esc")).Find("kbd");
        Assert.Equal("ib-kbd", kbd.ClassName);
        Assert.Equal("Esc", kbd.TextContent);
    }

    [Fact]
    public void Stat_shows_a_trend_without_change_text()
    {
        var change = Render<Stat>(p => p.Add(s => s.Label, "Orders").Add(s => s.Trend, StatTrend.Up)).Find(".ib-stat-change");
        Assert.NotNull(change.QuerySelector("svg"));
        Assert.Equal("Up ", change.QuerySelector(".ib-visually-hidden")!.TextContent);
    }
}
