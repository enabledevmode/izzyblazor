using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class SplitButtonTests : BunitContext
{
    private void Mode(bool interactive) => SetRendererInfo(new RendererInfo(interactive ? "Server" : "Static", interactive));

    private IRenderedComponent<SplitButton> RenderSplit(bool interactive, Action<ComponentParameterCollectionBuilder<SplitButton>> parameters)
    {
        Mode(interactive);
        return Render<SplitButton>(p =>
        {
            p.Add(s => s.Label, "Save").Add(s => s.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<MenuItem>(0);
                b.AddAttribute(1, nameof(MenuItem.Href), "/drafts");
                b.AddAttribute(2, nameof(MenuItem.ChildContent), (RenderFragment)(c => c.AddContent(0, "Drafts")));
                b.CloseComponent();
            }));
            parameters(p);
        });
    }

    private static IElement MainOf(IRenderedComponent<SplitButton> cut) => cut.Find(".ib-split-btn > .ib-btn");

    private static IElement ToggleOf(IRenderedComponent<SplitButton> cut) => cut.Find("ib-popover > .ib-btn");

    // Every attribute of an element, sorted. A handler's value is only its id, which each render gives anew.
    private static string[] Attributes(IElement element) =>
        [.. element.Attributes.Select(a => a.Name.StartsWith("blazor:", StringComparison.Ordinal) ? a.Name : $"{a.Name}={a.Value}").Order(StringComparer.Ordinal)];

    [Theory]
    [InlineData(true)]
    public void Renders_a_named_group_of_a_button_and_a_menu_on_an_arrow(bool interactive)
    {
        var cut = RenderSplit(interactive, p => p.Add(s => s.MenuLabel, "More save options"));

        var group = cut.Find("[role=group]");
        Assert.Equal("Save", group.GetAttribute("aria-label"));
        Assert.Equal("ib-btn-group ib-btn-group-attached ib-split-btn", group.ClassName);

        // Two controls, the main one first.
        Assert.Equal("Save", MainOf(cut).QuerySelector(".ib-btn-label")!.TextContent);
        Assert.Equal("button", MainOf(cut).GetAttribute("type"));
        Assert.Null(MainOf(cut).GetAttribute("popovertarget"));

        var toggle = ToggleOf(cut);
        var panel = cut.Find("[popover]");
        Assert.Equal("More save options", toggle.GetAttribute("aria-label"));
        Assert.Equal("More save options", toggle.GetAttribute("title"));
        Assert.Equal(panel.Id, toggle.GetAttribute("popovertarget"));
        Assert.Equal(toggle.Id, panel.GetAttribute("aria-labelledby"));
        Assert.Equal("menu", toggle.GetAttribute(interactive ? "aria-haspopup" : "data-ib-haspopup"));
        Assert.Equal("menu", panel.GetAttribute(interactive ? "role" : "data-ib-role"));
        Assert.Equal("/drafts", panel.QuerySelector("a.ib-menu-item")!.GetAttribute("href"));

        // The arrow's end lines up with the group's.
        Assert.Equal("bottom-start", cut.Find("ib-popover").GetAttribute("data-placement"));
        Assert.Contains("ib-place-bottom-start", panel.ClassList);
    }

    [Fact]
    public void The_arrows_name_defaults_from_the_label_and_IzzyText()
    {
        Assert.Equal("Save: more options", ToggleOf(RenderSplit(false, _ => { })).GetAttribute("aria-label"));
    }

    [Fact]
    public void A_submit_main_posts_what_a_button_would()
    {
        var main = MainOf(RenderSplit(false, p => p
            .Add(s => s.Submit, true)
            .Add(s => s.Name, "intent")
            .Add(s => s.Value, "save")
            .Add(s => s.FormAction, "/save")
            .Add(s => s.Form, "f")));

        Assert.Equal("submit", main.GetAttribute("type"));
        Assert.Equal("intent", main.GetAttribute("name"));
        Assert.Equal("save", main.GetAttribute("value"));
        Assert.Equal("/save", main.GetAttribute("formaction"));
        Assert.Equal("f", main.GetAttribute("form"));
    }

    [Fact]
    public void IconOnly_shows_the_icon_and_names_it_with_the_label()
    {
        var main = MainOf(RenderSplit(false, p => p.Add(s => s.StartIcon, Lucide.Save).Add(s => s.IconOnly, true)));

        Assert.Contains("ib-btn-icon-only", main.ClassList);
        Assert.Equal("Save", main.GetAttribute("aria-label"));
        Assert.Equal("Save", main.GetAttribute("title"));
        Assert.Null(main.QuerySelector(".ib-btn-label"));
        Assert.Single(main.QuerySelectorAll("svg"));
    }

    [Fact]
    public void The_main_click_and_an_item_each_run_their_own()
    {
        Mode(true);
        var cut = Render<SplitButtonProbe>(p => p.Add(x => x.Kind, "items"));

        cut.Find(".ib-split-btn > button").Click();
        Assert.Equal((1, 0), (cut.Instance.Clicks, cut.Instance.ItemClicks));

        cut.Find("[popover] button").Click();
        Assert.Equal((1, 1), (cut.Instance.Clicks, cut.Instance.ItemClicks));
    }

#pragma warning disable BL0006 // The render tree types, which this looks at on purpose.
    // Reads each render's frames. On a page a state change patches the element by comparing sequence
    // numbers, and an attribute out of order can then be lost; bUnit draws its markup from the frames
    // and can't show that, so this checks the order (the browser test checks the page itself).
    private sealed class FrameRenderer<TComponent>(IServiceProvider services) : Renderer(services, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
    {
        private int _rootId;
        private IComponent _root = null!;

        public List<string> OutOfOrder { get; } = [];

        public int AttributesSeen { get; private set; }

        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        protected override RendererInfo RendererInfo { get; } = new("Server", isInteractive: true);

        public Task MountAsync(ParameterView parameters) => Dispatcher.InvokeAsync(() =>
        {
            _root = InstantiateComponent(typeof(TComponent));
            _rootId = AssignRootComponentId(_root);
            return RenderRootComponentAsync(_rootId, parameters);
        });

        public Task SetAsync(ParameterView parameters) => Dispatcher.InvokeAsync(() => _root.SetParametersAsync(parameters));

        protected override void HandleException(Exception exception) => throw exception;

        protected override Task UpdateDisplayAsync(in RenderBatch batch)
        {
            for (var i = 0; i < batch.UpdatedComponents.Count; i++)
            {
                var frames = GetCurrentRenderTreeFrames(batch.UpdatedComponents.Array[i].ComponentId);
                var last = -1;
                for (var f = 0; f < frames.Count; f++)
                {
                    ref var frame = ref frames.Array[f];
                    if (frame.FrameType != RenderTreeFrameType.Attribute)
                    {
                        last = -1;
                        continue;
                    }

                    AttributesSeen++;
                    if (frame.Sequence < last)
                    {
                        OutOfOrder.Add($"{frame.AttributeName} at {frame.Sequence} after {last}");
                    }

                    last = frame.Sequence;
                }
            }

            return Task.CompletedTask;
        }
    }
#pragma warning restore BL0006

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Sequence_numbers_ascend_in_every_state(bool link, bool iconOnly)
    {
        ParameterView Parameters(bool disabled, bool loading) => ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(SplitButton.Label)] = "Report",
            [nameof(SplitButton.Href)] = link ? "/report.pdf" : null,
            [nameof(SplitButton.External)] = link,
            [nameof(SplitButton.Submit)] = !link,
            [nameof(SplitButton.Name)] = link ? null : "intent",
            [nameof(SplitButton.Value)] = link ? null : "save",
            [nameof(SplitButton.StartIcon)] = (IconData?)Lucide.Download,
            [nameof(SplitButton.IconOnly)] = iconOnly,
            [nameof(SplitButton.OnClick)] = EventCallback.Factory.Create<MouseEventArgs>(this, () => { }),
            [nameof(SplitButton.Disabled)] = disabled,
            [nameof(SplitButton.Loading)] = loading,
            ["data-x"] = "y",
        });

        using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new FrameRenderer<SplitButton>(services);
        await renderer.MountAsync(Parameters(false, false));
        foreach (var (disabled, loading) in new[] { (true, false), (false, false), (false, true), (false, false) })
        {
            await renderer.SetAsync(Parameters(disabled, loading));
        }

        Assert.True(renderer.AttributesSeen > 0);
        Assert.Empty(renderer.OutOfOrder);
    }

    private async Task AscendsAsync<TComponent>(params Dictionary<string, object?>[] states)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new FrameRenderer<TComponent>(services);
        await renderer.MountAsync(ParameterView.FromDictionary(states[0]));
        foreach (var state in states.Skip(1))
        {
            await renderer.SetAsync(ParameterView.FromDictionary(state));
        }

        Assert.True(renderer.AttributesSeen > 0);
        Assert.Empty(renderer.OutOfOrder);
    }

    private Dictionary<string, object?> ButtonState(string? href, bool disabled, bool loading) => new()
    {
        [nameof(Button.Href)] = href,
        [nameof(Button.Disabled)] = disabled,
        [nameof(Button.Loading)] = loading,
        [nameof(Button.OnClick)] = EventCallback.Factory.Create<MouseEventArgs>(this, () => { }),
        ["data-x"] = "y",
    };

    [Theory]
    [InlineData(null)]
    [InlineData("/report.pdf")]
    public Task A_buttons_sequence_numbers_ascend_in_every_state(string? href) => AscendsAsync<Button>(
        ButtonState(href, false, false),
        ButtonState(href, true, false),
        ButtonState(href, false, true),
        ButtonState(href, false, false));

    private IRenderedComponent<SplitButtonProbe> RenderProbe(string kind, bool interactive = false)
    {
        Mode(interactive);
        return Render<SplitButtonProbe>(p => p.Add(x => x.Kind, kind));
    }

    private static IElement MoreMenu(IRenderedComponent<SplitButtonProbe> cut) => cut.Find(".ib-toolbar-more [popover]");

    private static IElement MenuUnit(IRenderedComponent<SplitButtonProbe> cut, string text) =>
        MoreMenu(cut).QuerySelectorAll(".ib-toolbar-menu-unit").Single(u => u.TextContent.Contains(text, StringComparison.Ordinal));

    [Fact]
    public void In_the_menu_it_is_a_submenu_with_the_main_action_first()
    {
        var unit = MenuUnit(RenderProbe("toolbar"), "Save options");

        var trigger = unit.QuerySelector(".ib-menu-sub-trigger")!;
        Assert.Equal("Save options", trigger.QuerySelector(".ib-menu-label")!.TextContent.Trim());
        Assert.NotNull(trigger.QuerySelector("svg"));

        var items = unit.QuerySelectorAll(".ib-menu-sub > .ib-menu-item");
        Assert.Equal(["Save", "Drafts"], items.Select(i => i.TextContent.Trim()));

        var main = items[0];
        Assert.Equal("submit", main.GetAttribute("type"));
        Assert.Equal("intent", main.GetAttribute("name"));
        Assert.Equal("save", main.GetAttribute("value"));
        Assert.Equal("/save", main.GetAttribute("formaction"));
        Assert.Equal("f", main.GetAttribute("form"));
        Assert.Equal("/drafts", items[1].GetAttribute("href"));
    }

    [Fact]
    public void OnClick_runs_from_the_menu_copy_too()
    {
        var cut = RenderProbe("toolbar", interactive: true);

        MenuUnit(cut, "Save options").QuerySelector(".ib-menu-sub > button.ib-menu-item")!.Click();

        Assert.Equal(1, cut.Instance.Clicks);
    }

    private IToolbarItem Item(IRenderedComponent<SplitButtonProbe> cut, string label) =>
        cut.FindComponents<ToolbarSplitButton>().Single(c => c.Instance.Label == label).Instance;

    // Both parts: the main button (padding, border, text, icon) and a square arrow, less the shared border.
    [Fact]
    public void The_width_is_the_main_button_and_the_arrow_less_their_shared_border()
    {
        var small = RenderProbe("toolbar");

        // Sm: 12px padding and a 1px border each side, 16px icon, half the font's gap, a 32px arrow.
        var save = 2 * 12 + 2 + 16 + TextWidths.Of("Save", ControlSize.Sm) + 13 / 2.0 + 32 - 1;
        Assert.Equal(save, Item(small, "Save").Width!.Value, 3);

        // A link that opens a new tab has the external icon after its text.
        var export = 2 * 12 + 2 + TextWidths.Of("Export", ControlSize.Sm) + 12 + 13 / 2.0 + 32 - 1;
        Assert.Equal(export, Item(small, "Export").Width!.Value, 3);

        // With no collapsing, the same.
        Assert.Equal(Item(small, "Save").Width, Item(small, "Save").CollapsedWidth);
    }
}
