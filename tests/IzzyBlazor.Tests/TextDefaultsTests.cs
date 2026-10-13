using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class TextDefaultsTests : BunitContext
{
    // Renderer info reads the service provider, after which bUnit takes no more registrations.
    private void Static() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private sealed class Pirate : IzzyText
    {
        public override string Loading { get => "Hoistin'"; set { } }
    }

    [Fact]
    public void English_is_used_when_nothing_is_registered()
    {
        Static();
        Assert.Equal("Loading", Render<Spinner>().Find(".ib-visually-hidden").TextContent);
        Assert.Equal("Close", Render<Dialog>(p => p.Add(d => d.Title, "Hi")).Find(".ib-dialog-close").GetAttribute("aria-label"));
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
    public void A_subclass_can_supply_text_from_code()
    {
        Services.AddSingleton<IzzyText, Pirate>();
        Static();

        Assert.Equal("Hoistin'", Render<Spinner>().Find(".ib-visually-hidden").TextContent);
    }

    [Fact]
    public void A_component_parameter_wins_over_the_registered_text()
    {
        Services.AddSingleton(new IzzyText { Close = "Schließen" });
        Static();

        var close = Render<Dialog>(p => p.Add(d => d.Title, "Hallo").Add(d => d.CloseLabel, "Zumachen")).Find(".ib-dialog-close");
        Assert.Equal("Zumachen", close.GetAttribute("aria-label"));
    }

    [Fact]
    public void Format_strings_come_from_the_registered_text()
    {
        Services.AddSingleton(new IzzyText { MoreAvatars = "{0} weitere" });
        Static();

        var cut = Render<AvatarGroup>(p => p
            .Add(g => g.Max, 1)
            .AddChildContent<Avatar>(a => a.Add(x => x.Name, "Ada Lovelace"))
            .AddChildContent<Avatar>(a => a.Add(x => x.Name, "Grace Hopper"))
            .AddChildContent<Avatar>(a => a.Add(x => x.Name, "Alan Turing")));

        Assert.Contains("2 weitere", cut.Markup);
    }
}
