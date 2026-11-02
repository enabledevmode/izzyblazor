using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class DisplayRenderTests : BunitContext
{
    public DisplayRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    [Theory]
    [InlineData("Ctrl+K", new[] { "Ctrl", "K" })]
    [InlineData("Ctrl++", new[] { "Ctrl", "+" })]
    [InlineData("+", new[] { "+" })]
    public void Keys_split_on_plus(string keys, string[] expected) =>
        Assert.Equal(expected, Kbd.SplitKeys(keys));

    [Theory]
    [InlineData("TextField", "text field", HighlightMatch.WordStart, "[TextField]")]
    [InlineData("TextField", "field", HighlightMatch.WordStart, "Text[Field]")]
    [InlineData("TextField", "ield", HighlightMatch.WordStart, "TextField")]
    [InlineData("TextField", "ield", HighlightMatch.Anywhere, "TextF[ield]")]
    [InlineData("A form's post", "form, post!", HighlightMatch.WordStart, "A [form]'s [post]")]
    public void Highlight_marks_the_query_words(string text, string query, HighlightMatch match, string expected)
    {
        var marked = Highlight.Find(text, query, match).Aggregate(
            (Text: text, Shift: 0),
            (acc, range) =>
            {
                var (start, length) = range.GetOffsetAndLength(text.Length);
                var at = start + acc.Shift;
                return (acc.Text[..at] + "[" + acc.Text.Substring(at, length) + "]" + acc.Text[(at + length)..], acc.Shift + 2);
            }).Text;

        Assert.Equal(expected, marked);
    }

    [Fact]
    public void Highlight_marks_matches_and_encodes_the_text()
    {
        var cut = Render<Highlight>(p => p.Add(h => h.Text, "<b>Loading</b> state").Add(h => h.Query, "load"));

        Assert.Equal("Load", cut.Find("mark").TextContent);
        Assert.Equal("<b>Loading</b> state", cut.Find("span").TextContent);
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
}
