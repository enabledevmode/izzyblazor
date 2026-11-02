using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class NavigationRenderTests : BunitContext
{
    public NavigationRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private void GoTo(string path) => Services.GetRequiredService<BunitNavigationManager>().NavigateTo(path);

    private static RenderFragment ThreeTabs(bool disableFirst = false) => builder =>
    {
        foreach (var (title, disabled) in new[] { ("Profile", disableFirst), ("Billing history", false), ("Security", false) })
        {
            builder.OpenComponent<Tab>(0);
            builder.AddComponentParameter(1, nameof(Tab.Label), title);
            builder.AddComponentParameter(2, nameof(Tab.Disabled), disabled);
            builder.AddComponentParameter(3, nameof(Tab.ChildContent), (RenderFragment)(b => b.AddContent(0, $"{title} panel")));
            builder.CloseComponent();
        }
    };

    [Fact]
    public void Tabs_render_radios_labels_and_panels()
    {
        var cut = Render<Tabs>(p => p
            .Add(t => t.Id, "acct")
            .Add(t => t.Label, "Account")
            .Add(t => t.ChildContent, ThreeTabs()));

        var root = cut.Find("ib-tabs");
        Assert.Equal("ib-tabs ib-tabs-line", root.ClassName);
        Assert.Equal("profile", root.GetAttribute("data-selected"));
        Assert.Equal("Account", cut.Find(".ib-tabs-list").GetAttribute("aria-label"));

        var radios = cut.FindAll("input.ib-tab-input");
        Assert.Equal(new[] { "profile", "billing-history", "security" }, radios.Select(r => r.GetAttribute("value")));
        Assert.All(radios, r => Assert.Equal("acct", r.GetAttribute("name")));
        Assert.True(radios[0].HasAttribute("checked"));
        Assert.Equal("acct-billing-history-panel", radios[1].GetAttribute("aria-controls"));

        // With no script the tabs are a radio group; <ib-tabs> adds the tab roles.
        Assert.Equal("radiogroup", cut.Find(".ib-tabs-list").GetAttribute("role"));
        var panels = cut.FindAll(".ib-tab-panel");
        Assert.Equal(3, panels.Count);
        Assert.Equal("acct-billing-history-panel", panels[1].Id);
        Assert.Null(panels[1].GetAttribute("role"));
    }

    [Fact]
    public void Query_string_selects_the_tab()
    {
        GoTo("/settings?tab=security");
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Tabs")
            .Add(t => t.QueryKey, "tab")
            .Add(t => t.Selected, "profile")
            .Add(t => t.ChildContent, ThreeTabs()));

        Assert.Equal("security", cut.Find("ib-tabs").GetAttribute("data-selected"));
        Assert.Equal("tab", cut.Find("ib-tabs").GetAttribute("data-query-key"));
    }

    [Fact]
    public void Change_event_reports_the_tab_and_parent_can_select()
    {
        string? reported = null;
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Tabs")
            .Add(t => t.SelectedChanged, (string v) => reported = v)
            .Add(t => t.ChildContent, ThreeTabs()));

        cut.FindAll("input.ib-tab-input")[2].Change("security");
        Assert.Equal("security", reported);
        Assert.Equal("security", cut.Find("ib-tabs").GetAttribute("data-selected"));

        cut.Render(p => p.Add(t => t.Selected, "profile"));
        Assert.Equal("profile", cut.Find("ib-tabs").GetAttribute("data-selected"));
    }

    [Theory]
    [InlineData(1, 12, "1 2 3 4 5 … 12")]
    [InlineData(5, 12, "1 … 4 5 6 … 12")]
    [InlineData(12, 12, "1 … 8 9 10 11 12")]
    [InlineData(4, 7, "1 2 3 4 5 6 7")]
    public void Pagination_range_keeps_a_steady_width(int page, int count, string expected)
    {
        var items = Pagination.Range(page, count, siblings: 1, boundaries: 1).Select(i => i == 0 ? "…" : i.ToString());
        Assert.Equal(expected, string.Join(' ', items));
    }

    // What a phone shows: the numbers it doesn't hide, and every gap (a phone-only gap marks a run it hides).
    [Theory]
    [InlineData(1, 4, "1 … 4")]
    [InlineData(3, 4, "1 2 3 4")] // one number away from any gap stays: its gap would take the same room
    [InlineData(4, 7, "1 … 4 … 7")]
    [InlineData(5, 12, "1 … 5 … 12")]
    public void Pagination_on_phones_marks_every_page_it_leaves_out(int page, int count, string expected, int boundaries = 1)
    {
        var cut = Render<Pagination>(p => p.Add(x => x.Page, page).Add(x => x.TotalItems, count * 10).Add(x => x.PageSize, 10).Add(x => x.Boundaries, boundaries));

        var shown = cut.FindAll(".ib-pagination-list > li:not(:first-child, :last-child, .ib-pagination-far)")
            .Select(li => li.ClassList.Contains("ib-pagination-gap") ? "…" : li.TextContent.Replace("Page", "").Trim());
        Assert.Equal(expected, string.Join(' ', shown));
    }

    [Fact]
    public void Pagination_links_set_the_query_string()
    {
        GoTo("/items?page=3&sort=name");
        var cut = Render<Pagination>(p => p.Add(x => x.TotalItems, 95).Add(x => x.PageSize, 10));

        Assert.Equal("3", cut.Find("[aria-current=page]").TextContent.Replace("Page", "").Trim());
        Assert.Equal("http://localhost/items?page=4&sort=name", cut.Find("a.ib-pagination-next").GetAttribute("href"));
        Assert.Equal("http://localhost/items?sort=name", cut.FindAll("a.ib-pagination-item").First(a => a.TextContent.EndsWith("1")).GetAttribute("href"));
    }

    [Fact]
    public void Bound_pagination_uses_buttons_on_interactive_pages()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        int? reported = null;
        var cut = Render<Pagination>(p => p
            .Add(x => x.PageCount, 5)
            .Add(x => x.Page, 2)
            .Add(x => x.PageChanged, (int v) => reported = v));

        Assert.Empty(cut.FindAll("a"));
        cut.Find("button.ib-pagination-next").Click();
        Assert.Equal(3, reported);
    }

    [Fact]
    public void Breadcrumb_marks_the_current_page()
    {
        var cut = Render<Breadcrumb>(p => p.AddChildContent(b =>
        {
            b.OpenComponent<BreadcrumbItem>(0);
            b.AddComponentParameter(1, nameof(BreadcrumbItem.Href), "/");
            b.AddComponentParameter(2, nameof(BreadcrumbItem.ChildContent), (RenderFragment)(c => c.AddContent(0, "Home")));
            b.CloseComponent();
            b.OpenComponent<BreadcrumbItem>(3);
            b.AddComponentParameter(4, nameof(BreadcrumbItem.ChildContent), (RenderFragment)(c => c.AddContent(0, "Settings")));
            b.CloseComponent();
        }));

        Assert.Equal("Breadcrumb", cut.Find("nav").GetAttribute("aria-label"));
        Assert.Equal("/", cut.Find("a.ib-breadcrumb-link").GetAttribute("href"));
        Assert.Equal("page", cut.Find("span.ib-breadcrumb-current").GetAttribute("aria-current"));
    }
}
