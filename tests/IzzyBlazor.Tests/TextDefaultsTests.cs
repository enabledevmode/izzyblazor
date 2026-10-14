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
    public void Status_words_and_table_text_come_from_IzzyText()
    {
        Services.AddSingleton(new IzzyText { StatusWarning = "Achtung", TableName = "Tabelle", TablePages = "{0}: Seiten", TableRange = "{0}–{1} von {2}" });
        Static();

        Assert.Equal("Achtung: ", Render<Alert>(p => p.Add(a => a.Status, Status.Warning).AddChildContent("x")).Find(".ib-visually-hidden").TextContent);
        Assert.Equal("Achtung: ", Render<Banner>(p => p.Add(b => b.Status, Status.Warning).AddChildContent("x")).Find(".ib-visually-hidden").TextContent);

        var table = Render<Table<int>>(p => p.Add(t => t.Items, Enumerable.Range(1, 30)).Add(t => t.PageSize, 10)
            .Add(t => t.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<PropertyColumn<int, int>>(0);
                b.AddAttribute(1, "Property", (System.Linq.Expressions.Expression<Func<int, int>>)(i => i));
                b.CloseComponent();
            })));
        Assert.Equal("Tabelle", table.Find(".ib-table-scroll").GetAttribute("aria-label"));
        Assert.Equal("1–10 von 30", table.Find(".ib-table-summary").TextContent);
        Assert.Equal("Tabelle: Seiten", table.Find("nav").GetAttribute("aria-label"));
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

    [Fact]
    public void English_is_a_fresh_copy_so_changing_it_changes_nothing_shared()
    {
        IzzyText.English.Close = "Changed";
        Static();

        Assert.Equal("Close", IzzyText.English.Close);
        Assert.Equal("Close", Render<Dialog>(p => p.Add(d => d.Title, "Hi")).Find(".ib-dialog-close").GetAttribute("aria-label"));
    }
}
