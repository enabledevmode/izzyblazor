using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class SplitterRenderTests : BunitContext
{
    private readonly Cookies _cookies = new();

    public SplitterRenderTests()
    {
        Services.AddSingleton<IRequestCookies>(_cookies);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    private sealed class Cookies : IRequestCookies
    {
        public string? Value { get; set; }

        public string? Get(string name) => name == "ib-split-mail" ? Value : null;
    }

    private IRenderedComponent<Splitter> Mail(string? layoutKey = null) => Render<Splitter>(p => p
        .Add(s => s.LayoutKey, layoutKey)
        .AddChildContent<SplitterPane>(pane => pane.Add(x => x.Id, "folders").Add(x => x.Label, "Folders")
            .Add(x => x.Size, Length.Rem(16)).Add(x => x.Min, Length.Rem(12)).Add(x => x.Collapsible, true))
        .AddChildContent<SplitterPane>(pane => pane.Add(x => x.Id, "list").Add(x => x.Label, "Messages").Add(x => x.Size, Length.Percent(35)))
        .AddChildContent<SplitterPane>(pane => pane.Add(x => x.Id, "message").Add(x => x.Resizable, false)));

    [Fact]
    public void Panes_get_their_sizes_on_the_server_and_each_bar_resizes_the_pane_before_it()
    {
        var cut = Mail();

        var panes = cut.FindAll(".ib-split-pane");
        Assert.Equal("--ib-pane-size:16rem;--ib-pane-min:12rem;", panes[0].GetAttribute("style"));
        Assert.Equal("px", panes[0].GetAttribute("data-ib-unit"));
        Assert.Equal("%", panes[1].GetAttribute("data-ib-unit"));
        Assert.Contains("ib-split-fill", panes[2].ClassName);

        // Two bars: the first moves, named after Folders; the one beside a fixed pane doesn't.
        var bars = cut.FindAll(".ib-split-bar");
        Assert.Equal(2, bars.Count);
        Assert.Equal("separator", bars[0].GetAttribute("role"));
        Assert.Equal("vertical", bars[0].GetAttribute("aria-orientation"));
        Assert.Equal("folders", bars[0].GetAttribute("aria-controls"));
        Assert.Equal("Resize Folders", bars[0].GetAttribute("aria-label"));
        Assert.Null(bars[1].GetAttribute("role"));
        Assert.True(bars[1].HasAttribute("data-ib-fixed"));

        // Folders collapses from the first bar's button.
        var collapse = cut.Find(".ib-split-collapse");
        Assert.Equal("folders", collapse.GetAttribute("aria-controls"));
        Assert.Equal("Collapse Folders", collapse.GetAttribute("aria-label"));
    }

    [Fact]
    public void The_kept_layout_comes_from_the_cookie_and_a_damaged_one_is_ignored()
    {
        _cookies.Value = "!300px|40.5%|";
        var panes = Mail("mail").FindAll(".ib-split-pane");
        Assert.True(panes[0].HasAttribute("hidden"));
        Assert.StartsWith("--ib-pane-size:300px;", panes[0].GetAttribute("style"));
        Assert.StartsWith("--ib-pane-size:40.5%;", panes[1].GetAttribute("style"));

        _cookies.Value = "300px;color:red|x";
        panes = Mail("mail").FindAll(".ib-split-pane");
        Assert.False(panes[0].HasAttribute("hidden"));
        Assert.StartsWith("--ib-pane-size:16rem;", panes[0].GetAttribute("style"));
    }

    [Fact]
    public void A_pane_shown_later_between_two_takes_its_place_in_the_page()
    {
        var cut = Render<EditorWithPreview>();
        cut.Render(p => p.Add(x => x.ShowPreview, true));

        // Preview is created last, but sits between Editor and Output: the bars follow the page.
        var bars = cut.FindAll(".ib-split-bar");
        Assert.Equal(["Resize Editor", "Resize Preview"], bars.Select(b => b.GetAttribute("aria-label")));
    }

    [Fact]
    public void A_bar_beside_a_collapsible_pane_after_it_is_named_after_that_pane()
    {
        // Enter on the bar collapses Output, so the bar is about Output, and its value is Output's share.
        var cut = Render<Splitter>(p => p
            .Add(s => s.Orientation, Orientation.Vertical)
            .AddChildContent<SplitterPane>(pane => pane.Add(x => x.Label, "Editor"))
            .AddChildContent<SplitterPane>(pane => pane.Add(x => x.Id, "output").Add(x => x.Label, "Output")
                .Add(x => x.Size, Length.Percent(35)).Add(x => x.Collapsible, true)));

        var bar = cut.Find(".ib-split-bar");
        Assert.Equal("Resize Output", bar.GetAttribute("aria-label"));
        Assert.Equal("output", bar.GetAttribute("aria-controls"));
        Assert.Equal("35", bar.GetAttribute("aria-valuenow"));
    }

    private sealed class EditorWithPreview : ComponentBase
    {
        [Parameter] public bool ShowPreview { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<Splitter>(0);
            builder.AddComponentParameter(1, nameof(Splitter.ChildContent), (RenderFragment)(b =>
            {
                b.AddContent(0, Pane("Editor"));
                if (ShowPreview)
                {
                    b.AddContent(1, Pane("Preview"));
                }

                b.AddContent(2, Pane("Output"));
            }));
            builder.CloseComponent();
        }

        private static RenderFragment Pane(string label) => b =>
        {
            b.OpenComponent<SplitterPane>(0);
            b.AddComponentParameter(1, nameof(SplitterPane.Label), label);
            b.AddComponentParameter(2, nameof(SplitterPane.ChildContent), (RenderFragment)(c => c.AddContent(0, label)));
            b.CloseComponent();
        };
    }
}
