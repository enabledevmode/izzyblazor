using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class TextDefaultsTests : BunitContext
{
    // Renderer info reads the service provider, after which bUnit takes no more registrations.
    private void Static() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private sealed class Pirate : IzzyText
    {
        public override string Loading => "Hoistin'";
    }

    [Fact]
    public void A_registered_IzzyText_changes_the_defaults()
    {
        Services.AddSingleton(new IzzyText { Close = "Schließen", Pagination = "Seiten", PreviousPage = "Zurück" });
        Static();

        Assert.Equal("Schließen", Render<Dialog>(p => p.Add(d => d.Title, "Hallo")).Find(".ib-dialog-close").GetAttribute("aria-label"));
        var pages = Render<Pagination>(p => p.Add(x => x.PageCount, 3)).Find("nav");
        Assert.Equal("Seiten", pages.GetAttribute("aria-label"));
        Assert.Contains("Zurück", pages.InnerHtml);
    }

    [Fact]
    public void A_component_parameter_wins_over_the_registered_text()
    {
        Services.AddSingleton(new IzzyText { Close = "Schließen" });
        Static();

        var close = Render<Dialog>(p => p.Add(d => d.Title, "Hallo").Add(d => d.CloseLabel, "Zumachen")).Find(".ib-dialog-close");
        Assert.Equal("Zumachen", close.GetAttribute("aria-label"));
    }
}
