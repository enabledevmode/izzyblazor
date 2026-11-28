namespace IzzyBlazor.Tests;

public class SwatchTests : BunitContext
{
    [Fact]
    public void Swatch_shows_its_name_over_the_tokens_variable()
    {
        var named = Render<Swatch>(p => p.Add(s => s.Color, Color.PrimarySoft).Add(s => s.Label, "Brand soft"));
        Assert.Equal("background-color: var(--ib-color-primary-soft)", named.Find(".ib-swatch-chip").GetAttribute("style"));
        Assert.Equal("true", named.Find(".ib-swatch-chip").GetAttribute("aria-hidden"));
        Assert.Equal("Brand soft", named.Find(".ib-swatch-label").TextContent);
        Assert.Equal("--ib-color-primary-soft", named.Find(".ib-swatch-value").TextContent);

        var raw = Render<Swatch>(p => p.Add(s => s.Color, Color.Of("#0f766e")));
        Assert.Empty(raw.FindAll(".ib-swatch-label"));
        Assert.Equal("#0f766e", raw.Find(".ib-swatch-value").TextContent);
    }
}
