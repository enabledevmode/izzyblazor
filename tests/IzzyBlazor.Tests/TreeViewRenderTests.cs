using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class TreeViewRenderTests : BunitContext
{
    public TreeViewRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private sealed record Folder(string Id, Folder[] Folders);

    private static readonly Folder[] Folders =
    [
        new("docs", [new("finance", [new("invoices", [])]), new("contracts", [])]),
        new("photos", [new("2026", [])]),
    ];

    [Fact]
    public void A_tree_of_links_opens_the_current_items_branch_and_gives_linked_parents_their_own_button()
    {
        var cut = Render<TreeView<Folder>>(p => p
            .Add(t => t.Items, Folders).Add(t => t.ItemChildren, f => f.Folders).Add(t => t.ItemText, f => f.Id)
            .Add(t => t.ItemKey, f => f.Id).Add(t => t.ItemHref, f => $"files?folder={f.Id}").Add(t => t.ItemCurrent, f => f.Id == "invoices"));

        var open = cut.FindAll("details[open]").Select(d => d.ParentElement!.GetAttribute("data-ib-key"));
        Assert.Equal(["docs", "finance"], open);

        var current = cut.Find("a[aria-current]");
        Assert.Equal("page", current.GetAttribute("aria-current"));
        Assert.Equal("files?folder=invoices", current.GetAttribute("href"));

        // A linked parent: its link, then a summary of its own named by the item.
        var docs = cut.Find("li[data-ib-key='docs']");
        Assert.Equal("A", docs.Children[0].TagName);
        Assert.Equal("docs", docs.QuerySelector(":scope > details > summary.ib-tree-toggle")!.TextContent.Trim());
    }

    [Fact]
    public void A_plain_tree_opens_with_its_summaries_and_disabled_items_lose_their_links()
    {
        var cut = Render<TreeView<Folder>>(p => p
            .Add(t => t.Items, Folders).Add(t => t.ItemChildren, f => f.Folders).Add(t => t.ItemText, f => f.Id)
            .Add(t => t.ItemKey, f => f.Id).Add(t => t.ItemExpanded, f => f.Id == "photos"));

        Assert.Equal("photos", cut.Find("details[open]").ParentElement!.GetAttribute("data-ib-key"));
        Assert.Equal("docs", cut.Find("li[data-ib-key='docs'] > details > summary.ib-tree-item").TextContent.Trim());
        Assert.Equal("SPAN", cut.Find("li[data-ib-key='contracts'] > .ib-tree-item").TagName);

        var linked = Render<TreeView<Folder>>(p => p
            .Add(t => t.Items, Folders).Add(t => t.ItemKey, f => f.Id).Add(t => t.ItemText, f => f.Id)
            .Add(t => t.ItemHref, f => f.Id).Add(t => t.ItemDisabled, f => f.Id == "photos"));
        Assert.Equal(["docs"], linked.FindAll("a").Select(a => a.TextContent.Trim()));
    }

    [Fact]
    public void A_static_render_checks_the_posted_keys_and_opens_and_mixes_the_branches_holding_them()
    {
        var cut = Render<TreeView<Folder>>(p => p
            .Add(t => t.Items, Folders).Add(t => t.ItemChildren, f => f.Folders).Add(t => t.ItemText, f => f.Id)
            .Add(t => t.ItemKey, f => f.Id).Add(t => t.SelectionMode, SelectionMode.Multiple)
            .Add(t => t.SelectionName, "granted").Add(t => t.Selection, Selection.Of("finance")));

        var boxes = cut.FindAll("input[type=checkbox][name='granted.Keys']");
        // What was posted: with no script, a parent's box alone. The script checks its branch.
        Assert.Equal(["finance"], boxes.Where(b => b.HasAttribute("checked")).Select(b => b.GetAttribute("value")));
        Assert.Equal("mixed", cut.Find("li[data-ib-key='docs']").GetAttribute("data-ib-checked"));
        Assert.Equal(["docs"], cut.FindAll("details[open]").Select(d => d.ParentElement!.GetAttribute("data-ib-key")));
        Assert.Equal("multiple", cut.Find("ib-tree").GetAttribute("data-ib-select"));
    }

    [Fact]
    public void A_page_fetch_for_a_branch_answers_with_only_that_branchs_children()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("files?ib-suggest=tree&ib-suggest-q=docs");
        string? asked = null;
        var cut = Render<TreeView<Folder>>(p => p
            .Add(t => t.Items, Folders).Add(t => t.ItemKey, f => f.Id).Add(t => t.ItemText, f => f.Id)
            .Add(t => t.ItemHasChildren, f => f.Id is "docs" or "photos")
            .Add(t => t.ChildrenProvider, (key, _) => { asked = key; return ValueTask.FromResult<IEnumerable<Folder>>([new("contracts", [])]); }));

        Assert.Equal("docs", asked);
        Assert.Equal("tree", cut.Find("ib-tree").GetAttribute("data-ib-tree-key"));
        Assert.Equal(["contracts"], cut.FindAll("ib-tree > ul[data-ib-answered] > li").Select(li => li.GetAttribute("data-ib-key")));
    }
}
