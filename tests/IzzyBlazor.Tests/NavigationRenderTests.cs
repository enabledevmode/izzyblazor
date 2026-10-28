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
    public void Duplicate_and_non_latin_titles_get_unique_keys()
    {
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Tabs")
            .Add(t => t.Id, "t")
            .Add(t => t.ChildContent, (RenderFragment)(b =>
            {
                foreach (var title in new[] { "Профиль", "Профиль", "Настройки" })
                {
                    b.OpenComponent<Tab>(0);
                    b.AddComponentParameter(1, nameof(Tab.Label), title);
                    b.CloseComponent();
                }
            })));

        var values = cut.FindAll("input.ib-tab-input").Select(r => r.GetAttribute("value")).ToArray();
        Assert.Equal(new[] { "профиль", "профиль-2", "настройки" }, values);
        Assert.Single(cut.FindAll("input.ib-tab-input[checked]"));
    }

    [Fact]
    public void Tab_list_follows_changes_to_a_tab()
    {
        var badge = "1";
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Tabs").Add(t => t.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<Tab>(0);
            b.AddComponentParameter(1, nameof(Tab.Label), "Inbox");
            b.AddComponentParameter(2, nameof(Tab.Badge), badge);
            b.CloseComponent();
        })));
        Assert.Equal("1", cut.Find(".ib-tab .ib-badge").TextContent.Trim());

        badge = "2";
        cut.Render();

        Assert.Equal("2", cut.Find(".ib-tab .ib-badge").TextContent.Trim());
    }

    [Fact]
    public void First_enabled_tab_is_selected_by_default()
    {
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Tabs").Add(t => t.ChildContent, ThreeTabs(disableFirst: true)));

        var radios = cut.FindAll("input.ib-tab-input");
        Assert.True(radios[0].HasAttribute("disabled"));
        Assert.True(radios[1].HasAttribute("checked"));
        Assert.Contains("ib-tab-disabled", cut.FindAll(".ib-tab")[0].ClassName);
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
    public void Interactive_tab_follows_the_address_without_navigating_itself()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        GoTo("/settings?tab=profile");
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        var historyBefore = nav.History.Count;
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Tabs")
            .Add(t => t.QueryKey, "tab")
            .Add(t => t.ChildContent, ThreeTabs()));

        // A click: the script writes the URL; no navigation (which would reload an island page).
        Assert.Equal("tab", cut.Find("ib-tabs").GetAttribute("data-query-key"));
        cut.FindAll("input.ib-tab-input")[1].Change(true);
        Assert.Equal(historyBefore, nav.History.Count);

        // A link to ?tab=security on the same page selects it.
        nav.NavigateTo("/settings?tab=security");
        cut.WaitForAssertion(() => Assert.Equal("security", cut.Find("ib-tabs").GetAttribute("data-selected")));
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
    [InlineData(4, 8, "1 2 3 4 5 … 8")]
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
    [InlineData(1, 12, "1 … 12")]
    [InlineData(5, 12, "1 … 5 … 12")]
    [InlineData(22, 24, "1 … 22 23 24")]
    [InlineData(1, 10, "1 …", 0)] // no boundaries: the window's far edge isn't kept as if it were the last page
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
    public void Pagination_disables_previous_on_the_first_page()
    {
        var cut = Render<Pagination>(p => p.Add(x => x.PageCount, 3).Add(x => x.Page, 1));

        var prev = cut.Find(".ib-pagination-prev");
        Assert.Equal("span", prev.TagName.ToLowerInvariant());
        Assert.Equal("true", prev.GetAttribute("aria-disabled"));
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
