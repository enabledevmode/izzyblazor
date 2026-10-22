using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

// What screen readers get where a component used to leave something unnamed, unread or
// unreachable: one test per fix, on the markup that carries it.
public class AccessibleNameTests : BunitContext
{
    public AccessibleNameTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    [Fact]
    public void A_code_block_is_a_named_group_with_or_without_a_title()
    {
        var plain = Render<Code>(p => p.Add(c => c.Block, true).AddChildContent("dotnet run")).Find("pre");
        var titled = Render<Code>(p => p.Add(c => c.Block, true).Add(c => c.Title, "Program.cs").AddChildContent("x")).Find("pre");

        Assert.Equal(("group", "Code"), (plain.GetAttribute("role"), plain.GetAttribute("aria-label")));
        Assert.Equal("group", titled.GetAttribute("role"));
        Assert.Null(titled.GetAttribute("aria-label"));
        Assert.NotNull(titled.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void An_aria_label_on_Progress_names_the_bar()
    {
        var cut = Render<Progress>(p => p.Add(x => x.Value, 40).AddUnmatched("aria-label", "Upload"));

        Assert.Equal("Upload", cut.Find("[role=progressbar]").GetAttribute("aria-label"));
        Assert.Null(cut.Find(".ib-progress").GetAttribute("aria-label"));
    }

    [Fact]
    public void A_loading_IconButton_says_so_and_is_disabled_to_screen_readers()
    {
        var button = Render<IconButton>(p => p
            .Add(b => b.Icon, Lucide.Trash)
            .Add(b => b.Label, "Delete")
            .Add(b => b.Loading, true)).Find("button");

        Assert.Equal("Loading Delete", button.GetAttribute("aria-label"));
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.Equal("Delete", button.GetAttribute("title"));
    }

    [Fact]
    public void A_labeled_Divider_is_read_as_its_text()
    {
        var labeled = Render<Divider>(p => p.Add(d => d.Label, "or")).Find(".ib-divider");
        var vertical = Render<Divider>(p => p.Add(d => d.Orientation, Orientation.Vertical)).Find(".ib-divider");

        Assert.Null(labeled.GetAttribute("role"));
        Assert.Equal("or", labeled.TextContent);
        Assert.Equal("separator", vertical.GetAttribute("role"));
    }

    [Fact]
    public void Timeline_keeps_its_list_role()
    {
        Assert.Equal("list", Render<Timeline>().Find("ol").GetAttribute("role"));
    }

    [Fact]
    public void Fieldset_description_describes_the_group()
    {
        var cut = Render<Fieldset>(p => p.Add(f => f.Label, "Shipping").Add(f => f.Description, "Where we send it."));
        var description = cut.Find(".ib-fieldset-description");

        Assert.Equal(description.Id, cut.Find("fieldset").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void A_loading_ConfirmButton_doesnt_open_its_dialog()
    {
        var trigger = Render<ConfirmButton>(p => p.Add(b => b.Title, "Delete?").Add(b => b.Loading, true).AddChildContent("Delete")).Find("button");

        Assert.Null(trigger.GetAttribute("commandfor"));
        Assert.Null(trigger.GetAttribute("command"));
    }

    [Fact]
    public void A_linked_Card_needs_a_title()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render<Card>(p => p.Add(c => c.Href, "/x")));

        Assert.Contains("Title", error.Message);
    }

    [Fact]
    public void AppMain_inside_a_shell_keeps_the_skip_links_target()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render<AppShell>(p => p.AddChildContent<AppMain>(m => m.Add(x => x.Id, "content"))));

        Assert.Contains("MainId", error.Message);
    }
}
