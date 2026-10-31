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
    [InlineData(false)]
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
    public void A_registered_IzzyText_changes_the_arrows_name()
    {
        Services.AddSingleton(new IzzyText { SplitButtonMenu = "Mehr zu {0}" });

        Assert.Equal("Mehr zu Save", ToggleOf(RenderSplit(false, _ => { })).GetAttribute("aria-label"));
    }

    [Fact]
    public void A_link_main_keeps_its_attributes_and_other_attributes_go_on_it()
    {
        var cut = RenderSplit(false, p => p
            .Add(s => s.Href, "/report.pdf")
            .Add(s => s.External, true)
            .Add(s => s.Class, "mine")
            .Add(s => s.Width, Length.Rem(20))
            .AddUnmatched("download", "report.pdf")
            .AddUnmatched("id", "main"));

        var main = MainOf(cut);
        Assert.Equal("A", main.TagName);
        Assert.Equal("/report.pdf", main.GetAttribute("href"));
        Assert.Equal("report.pdf", main.GetAttribute("download"));
        Assert.Equal("main", main.Id);
        Assert.Equal("_blank", main.GetAttribute("target"));
        Assert.Contains("noopener", main.GetAttribute("rel"));
        Assert.Contains("opens in a new tab", main.TextContent);

        // Class and the layout props go on the group its parent lays out; the arrow gets none of these.
        var group = cut.Find("[role=group]");
        Assert.Contains("mine", group.ClassList);
        Assert.Contains("20rem", group.GetAttribute("style"));
        Assert.Null(ToggleOf(cut).GetAttribute("download"));
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
    public void A_link_can_not_also_submit()
    {
        Assert.Throws<InvalidOperationException>(() => RenderSplit(false, p => p.Add(s => s.Href, "/x").Add(s => s.Submit, true)));
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
    public void An_IconOnly_name_says_what_the_hidden_texts_would()
    {
        var external = MainOf(RenderSplit(false, p => p.Add(s => s.StartIcon, Lucide.Save).Add(s => s.IconOnly, true).Add(s => s.Href, "https://x.test").Add(s => s.External, true)));
        Assert.Equal("Save (opens in a new tab)", external.GetAttribute("aria-label"));
        Assert.Single(external.QuerySelectorAll("svg"));

        var loading = MainOf(RenderSplit(false, p => p.Add(s => s.StartIcon, Lucide.Save).Add(s => s.IconOnly, true).Add(s => s.Loading, true)));
        Assert.Equal("Loading Save", loading.GetAttribute("aria-label"));
    }

    [Fact]
    public void An_app_can_override_the_IconOnly_name()
    {
        var main = MainOf(RenderSplit(false, p => p.Add(s => s.StartIcon, Lucide.Save).Add(s => s.IconOnly, true).AddUnmatched("aria-label", "Mine")));

        Assert.Equal("Mine", main.GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void It_needs_a_label(string label)
    {
        Mode(false);
        var error = Assert.Throws<InvalidOperationException>(() => Render<SplitButton>(p => p.Add(s => s.Label, label)));
        Assert.Contains("Label", error.Message);
    }

    [Fact]
    public void IconOnly_needs_an_icon()
    {
        var error = Assert.Throws<InvalidOperationException>(() => RenderSplit(false, p => p.Add(s => s.IconOnly, true)));
        Assert.Contains("StartIcon", error.Message);
    }

    [Theory]
    [InlineData(true, false, false, true, true)]
    [InlineData(false, true, false, true, false)]
    [InlineData(false, false, true, false, true)]
    [InlineData(false, false, false, false, false)]
    public void Disabled_is_both_and_the_half_ones_are_one(bool disabled, bool main, bool menu, bool mainOff, bool toggleOff)
    {
        var cut = RenderSplit(false, p => p.Add(s => s.Disabled, disabled).Add(s => s.MainDisabled, main).Add(s => s.MenuDisabled, menu));

        Assert.Equal(mainOff, MainOf(cut).HasAttribute("disabled"));
        Assert.Equal(toggleOff, ToggleOf(cut).HasAttribute("disabled"));
    }

    [Fact]
    public void Loading_blocks_both_parts_and_both_keep_focus()
    {
        var cut = RenderSplit(true, p => p.Add(s => s.Loading, true));

        var main = MainOf(cut);
        Assert.False(main.HasAttribute("disabled"));
        Assert.Equal("true", main.GetAttribute("aria-busy"));
        Assert.Equal("true", main.GetAttribute("aria-disabled"));
        Assert.Contains("ib-btn-loading", main.ClassList);

        // Not "disabled", so it keeps focus; nothing to open the menu with.
        var toggle = ToggleOf(cut);
        Assert.False(toggle.HasAttribute("disabled"));
        Assert.Equal("true", toggle.GetAttribute("aria-disabled"));
        Assert.Null(toggle.GetAttribute("popovertarget"));
        Assert.NotNull(cut.Find("[popover]").Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_loading_link_stays_a_tab_stop_with_no_href_and_gives_it_back(bool iconOnly)
    {
        Mode(true);
        void Parameters(ComponentParameterCollectionBuilder<SplitButton> p, bool loading) => p
            .Add(s => s.Label, "Report").Add(s => s.Href, "/r").Add(s => s.StartIcon, Lucide.Download).Add(s => s.IconOnly, iconOnly).Add(s => s.Loading, loading);
        var cut = Render<SplitButton>(p => Parameters(p, false));
        Assert.Null(MainOf(cut).GetAttribute("tabindex"));

        cut.Render(p => Parameters(p, true));
        Assert.Equal("0", MainOf(cut).GetAttribute("tabindex"));
        Assert.Null(MainOf(cut).GetAttribute("href"));

        cut.Render(p => Parameters(p, false));
        Assert.Null(MainOf(cut).GetAttribute("tabindex"));
        Assert.Equal("/r", MainOf(cut).GetAttribute("href"));
    }

    [Fact]
    public void A_disabled_link_is_not_a_tab_stop()
    {
        Assert.Null(MainOf(RenderSplit(false, p => p.Add(s => s.Href, "/r").Add(s => s.Disabled, true))).GetAttribute("tabindex"));
    }

    [Theory]
    [InlineData("EndIcon")]
    [InlineData("Pressed")]
    [InlineData("PressedChanged")]
    [InlineData("Reset")]
    [InlineData("HideExternalIcon")]
    [InlineData("endicon")]
    public void Button_parameters_that_make_no_sense_here_are_refused(string name)
    {
        var error = Assert.Throws<InvalidOperationException>(() => RenderSplit(false, p => p.AddUnmatched(name, true)));

        Assert.Contains("SplitButton doesn't take", error.Message);
    }

    [Fact]
    public void FullWidth_goes_to_the_group()
    {
        var group = RenderSplit(false, p => p.Add(s => s.FullWidth, true)).Find("[role=group]");

        Assert.Contains("ib-btn-group-full", group.ClassList);
        Assert.DoesNotContain("ib-btn-full", group.QuerySelector(".ib-btn")!.ClassList);
    }

    [Fact]
    public void The_screen_reader_labels_reach_the_main_button()
    {
        var loading = MainOf(RenderSplit(false, p => p.Add(s => s.Loading, true).Add(s => s.LoadingLabel, "Busy")));
        Assert.Equal("Busy", loading.QuerySelector(".ib-visually-hidden")!.TextContent);

        var link = MainOf(RenderSplit(false, p => p.Add(s => s.Href, "/x").Add(s => s.External, true).Add(s => s.NewTabLabel, "(new tab)")));
        Assert.Contains("(new tab)", link.TextContent);

        var named = MainOf(RenderSplit(false, p => p.Add(s => s.StartIcon, Lucide.Save).Add(s => s.IconOnly, true).Add(s => s.Loading, true).Add(s => s.LoadingLabel, "Busy")));
        Assert.Equal("Busy Save", named.GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_MenuLabel_takes_the_default(string label)
    {
        Assert.Equal("Save: more options", ToggleOf(RenderSplit(false, p => p.Add(s => s.MenuLabel, label))).GetAttribute("aria-label"));
    }

    [Fact]
    public void The_look_comes_from_the_group_around_it_unless_set()
    {
        Mode(false);
        var groups = Render<SplitButtonProbe>(p => p.Add(x => x.Kind, "in-group")).FindAll(".ib-split-btn");

        foreach (var button in groups[0].QuerySelectorAll(".ib-btn"))
        {
            Assert.Contains("ib-btn-neutral", button.ClassList);
            Assert.Contains("ib-btn-outline", button.ClassList);
            Assert.Contains("ib-btn-sm", button.ClassList);
        }

        foreach (var button in groups[1].QuerySelectorAll(".ib-btn"))
        {
            Assert.Contains("ib-btn-danger", button.ClassList);
            Assert.Contains("ib-btn-solid", button.ClassList);
            Assert.Contains("ib-btn-lg", button.ClassList);
        }
    }

    [Fact]
    public void Without_a_group_it_looks_like_a_button()
    {
        var cut = RenderSplit(false, _ => { });

        Assert.Equal("ib-btn ib-btn-primary ib-btn-solid", MainOf(cut).ClassName);
        Assert.Equal("ib-btn ib-btn-primary ib-btn-solid ib-btn-icon-only", ToggleOf(cut).ClassName);
    }

    [Fact]
    public void Placement_and_panel_width_reach_the_menu()
    {
        var cut = RenderSplit(false, p => p.Add(s => s.Placement, Placement.TopStart).Add(s => s.PanelWidth, Length.Rem(18)));

        Assert.Equal("top-start", cut.Find("ib-popover").GetAttribute("data-placement"));
        Assert.Contains("18rem", cut.Find("[popover]").GetAttribute("style"));
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

    private Dictionary<string, object?> ButtonState(string? href, bool? pressed, bool disabled, bool loading) => new()
    {
        [nameof(Button.Href)] = href,
        [nameof(Button.Pressed)] = href is null ? pressed : null,
        [nameof(Button.Disabled)] = disabled,
        [nameof(Button.Loading)] = loading,
        [nameof(Button.OnClick)] = EventCallback.Factory.Create<MouseEventArgs>(this, () => { }),
        ["data-x"] = "y",
    };

    [Theory]
    [InlineData(null)]
    [InlineData("/report.pdf")]
    public Task A_buttons_sequence_numbers_ascend_in_every_state(string? href) => AscendsAsync<Button>(
        ButtonState(href, true, false, false),
        ButtonState(href, null, false, false),
        ButtonState(href, false, true, false),
        ButtonState(href, true, false, true),
        ButtonState(href, null, false, false));

    [Theory]
    [InlineData(null)]
    [InlineData("/report.pdf")]
    public Task An_icon_buttons_sequence_numbers_ascend_in_every_state(string? href)
    {
        Dictionary<string, object?> State(bool? pressed, bool disabled, bool loading)
        {
            var state = ButtonState(href, pressed, disabled, loading);
            state[nameof(IconButton.Icon)] = Lucide.Download;
            state[nameof(IconButton.Label)] = "Report";
            return state;
        }

        return AscendsAsync<IconButton>(State(true, false, false), State(null, false, false), State(false, true, false), State(true, false, true), State(null, false, false));
    }

    // On each state: what a fresh render has. The order above is what keeps this true on a page.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_link_keeps_every_attribute_through_state_changes(bool iconOnly)
    {
        Mode(true);
        void Parameters(ComponentParameterCollectionBuilder<SplitButton> p, bool disabled, bool loading) => p
            .Add(s => s.Label, "Report")
            .Add(s => s.Href, "/report.pdf")
            .Add(s => s.External, true)
            .Add(s => s.StartIcon, Lucide.Download)
            .Add(s => s.IconOnly, iconOnly)
            .Add(s => s.OnClick, (MouseEventArgs _) => { })
            .Add(s => s.Class, "mine")
            .Add(s => s.Disabled, disabled)
            .Add(s => s.Loading, loading)
            .AddUnmatched("download", "report.pdf")
            .AddUnmatched("data-x", "y");

        var fresh = new Dictionary<(bool, bool), string[]>();
        foreach (var state in new[] { (false, false), (true, false), (false, true) })
        {
            fresh[state] = Attributes(MainOf(Render<SplitButton>(p => Parameters(p, state.Item1, state.Item2))));
        }

        // The enabled link has what it should, so the comparisons below mean something.
        Assert.Contains("href=/report.pdf", fresh[(false, false)]);
        Assert.Contains("download=report.pdf", fresh[(false, false)]);
        Assert.Contains("data-x=y", fresh[(false, false)]);
        Assert.Contains(fresh[(false, false)], a => a.StartsWith("class=", StringComparison.Ordinal));
        Assert.DoesNotContain("href=/report.pdf", fresh[(true, false)]);
        Assert.Contains("aria-disabled=true", fresh[(true, false)]);

        var cut = Render<SplitButton>(p => Parameters(p, false, false));
        foreach (var state in new[] { (true, false), (false, false), (false, true), (false, false), (true, false), (false, true), (true, false), (false, false) })
        {
            cut.Render(p => Parameters(p, state.Item1, state.Item2));
            Assert.Equal(fresh[state], Attributes(MainOf(cut)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_button_keeps_every_attribute_through_state_changes(bool iconOnly)
    {
        Mode(true);
        void Parameters(ComponentParameterCollectionBuilder<SplitButton> p, bool disabled, bool loading) => p
            .Add(s => s.Label, "Save")
            .Add(s => s.Submit, true)
            .Add(s => s.Name, "intent")
            .Add(s => s.Value, "save")
            .Add(s => s.FormAction, "/save")
            .Add(s => s.Form, "f")
            .Add(s => s.StartIcon, Lucide.Save)
            .Add(s => s.IconOnly, iconOnly)
            .Add(s => s.OnClick, (MouseEventArgs _) => { })
            .Add(s => s.Disabled, disabled)
            .Add(s => s.Loading, loading)
            .AddUnmatched("data-x", "y");

        var fresh = new Dictionary<(bool, bool), string[]>();
        foreach (var state in new[] { (false, false), (true, false), (false, true) })
        {
            fresh[state] = Attributes(MainOf(Render<SplitButton>(p => Parameters(p, state.Item1, state.Item2))));
        }

        var cut = Render<SplitButton>(p => Parameters(p, false, false));
        foreach (var state in new[] { (true, false), (false, false), (false, true), (false, false), (true, false), (false, true), (true, false), (false, false) })
        {
            cut.Render(p => Parameters(p, state.Item1, state.Item2));
            Assert.Equal(fresh[state], Attributes(MainOf(cut)));
        }
    }

    [Fact]
    public void The_arrow_keeps_its_wiring_through_state_changes()
    {
        Mode(true);
        var cut = Render<SplitButton>(p => p.Add(s => s.Label, "Save"));
        var before = Attributes(ToggleOf(cut));
        Assert.Contains(before, a => a.StartsWith("popovertarget=", StringComparison.Ordinal));

        foreach (var (disabled, loading, menu) in new[] { (true, false, false), (false, false, false), (false, true, false), (false, false, false), (false, false, true), (false, true, false), (false, false, false) })
        {
            cut.Render(p => p.Add(s => s.Disabled, disabled).Add(s => s.Loading, loading).Add(s => s.MenuDisabled, menu));
            if (!disabled && !loading && !menu)
            {
                Assert.Equal(before, Attributes(ToggleOf(cut)));
            }
        }
    }

    [Fact]
    public void In_a_link_to_button_change_the_new_element_has_everything()
    {
        Mode(true);
        var cut = Render<SplitButton>(p => p.Add(s => s.Label, "Save").Add(s => s.Href, "/x").AddUnmatched("data-x", "y"));
        Assert.Equal("A", MainOf(cut).TagName);

        cut.Render(p => p.Add(s => s.Href, null).Add(s => s.Submit, true));

        Assert.Equal("BUTTON", MainOf(cut).TagName);
        Assert.Equal("submit", MainOf(cut).GetAttribute("type"));
        Assert.Equal("y", MainOf(cut).GetAttribute("data-x"));
        Assert.Null(MainOf(cut).GetAttribute("href"));
    }

    private IRenderedComponent<SplitButtonProbe> RenderProbe(string kind, bool interactive = false)
    {
        Mode(interactive);
        return Render<SplitButtonProbe>(p => p.Add(x => x.Kind, kind));
    }

    private static IElement MoreMenu(IRenderedComponent<SplitButtonProbe> cut) => cut.Find(".ib-toolbar-more [popover]");

    private static IElement MenuUnit(IRenderedComponent<SplitButtonProbe> cut, string text) =>
        MoreMenu(cut).QuerySelectorAll(".ib-toolbar-menu-unit").Single(u => u.TextContent.Contains(text, StringComparison.Ordinal));

    [Fact]
    public void In_the_bar_it_takes_the_toolbars_look_and_has_its_own_menu()
    {
        var cut = RenderProbe("toolbar");
        var unit = cut.FindAll(".ib-toolbar-unit")[0];

        var group = unit.QuerySelector(".ib-split-btn")!;
        foreach (var button in group.QuerySelectorAll(".ib-btn"))
        {
            Assert.Contains("ib-btn-neutral", button.ClassList);
            Assert.Contains("ib-btn-ghost", button.ClassList);
            Assert.Contains("ib-btn-sm", button.ClassList);
        }

        // The panel in the bar is the bar's, with its own item; the unit carries where it sits.
        Assert.Equal("Drafts", unit.QuerySelector("[popover] .ib-menu-label")!.TextContent.Trim());
        Assert.Equal("2", cut.FindAll(".ib-toolbar-unit").Single(u => u.TextContent.Contains("Export", StringComparison.Ordinal)).GetAttribute("data-ib-priority"));
        Assert.Equal("always", cut.FindAll(".ib-toolbar-unit").Single(u => u.TextContent.Contains("Export", StringComparison.Ordinal)).GetAttribute("data-ib-overflow"));
        Assert.NotNull(cut.Find(".ib-toolbar-end .ib-split-btn"));
    }

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
    public void A_link_main_in_the_menu_keeps_download_and_its_new_tab()
    {
        var main = MenuUnit(RenderProbe("toolbar"), "Formats").QuerySelector(".ib-menu-sub > a.ib-menu-item")!;

        Assert.Equal("/export.csv", main.GetAttribute("href"));
        Assert.Equal("data.csv", main.GetAttribute("download"));
        Assert.Equal("_blank", main.GetAttribute("target"));
        Assert.Contains("noopener", main.GetAttribute("rel"));
    }

    [Fact]
    public void OnClick_runs_from_the_menu_copy_too()
    {
        var cut = RenderProbe("toolbar", interactive: true);

        MenuUnit(cut, "Save options").QuerySelector(".ib-menu-sub > button.ib-menu-item")!.Click();

        Assert.Equal(1, cut.Instance.Clicks);
    }

    [Fact]
    public void With_MenuDisabled_the_menu_copy_is_just_the_main_action()
    {
        var unit = MenuUnit(RenderProbe("toolbar"), "Delete");

        Assert.Empty(unit.QuerySelectorAll("ib-popover"));
        Assert.DoesNotContain("Hidden", unit.TextContent);
        Assert.Contains("ib-menu-item-danger", unit.QuerySelector(".ib-menu-item")!.ClassList);
        Assert.False(unit.QuerySelector(".ib-menu-item")!.HasAttribute("disabled"));
    }

    [Fact]
    public void MainDisabled_disables_the_first_item()
    {
        Mode(false);
        var unit = Render<SplitButtonProbe>(p => p.Add(x => x.Kind, "toolbar").Add(x => x.MainDisabled, true));

        Assert.True(MenuUnit(unit, "Delete").QuerySelector(".ib-menu-item")!.HasAttribute("disabled"));
    }

    [Theory]
    [InlineData("Off options")]
    [InlineData("Busy options")]
    public void Disabled_and_loading_disable_the_submenu(string label)
    {
        var trigger = MenuUnit(RenderProbe("toolbar"), label).QuerySelector(".ib-menu-sub-trigger")!;

        Assert.True(trigger.HasAttribute("disabled"));
    }

    [Fact]
    public void One_wired_up_by_an_attribute_stays_in_the_bar()
    {
        var cut = RenderProbe("wired");

        Assert.DoesNotContain("Wired", MoreMenu(cut).TextContent);
        Assert.Equal("x", cut.Find(".ib-split-btn > .ib-btn").GetAttribute("data-command"));
    }

    [Fact]
    public void CollapseLabels_collapses_the_main_button_and_keeps_the_arrow()
    {
        var groups = RenderProbe("collapse").FindAll(".ib-toolbar-unit .ib-split-btn");

        // A start icon: collapses. No icon, or Never: keeps its text.
        Assert.Contains("ib-toolbar-collapse", groups[0].QuerySelector(":scope > .ib-btn")!.ClassList);
        Assert.Contains("ib-toolbar-collapse-square", groups[0].QuerySelector(":scope > .ib-btn")!.ClassList);
        Assert.DoesNotContain("ib-toolbar-collapse", groups[1].QuerySelector(":scope > .ib-btn")!.ClassList);
        Assert.DoesNotContain("ib-toolbar-collapse", groups[2].QuerySelector(":scope > .ib-btn")!.ClassList);
        Assert.DoesNotContain("ib-toolbar-collapse", groups[0].QuerySelector("ib-popover > .ib-btn")!.ClassList);
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

    [Fact]
    public void Icon_only_and_collapsed_are_two_squares_less_the_shared_border()
    {
        var cut = RenderProbe("collapse");

        // Md: 36px squares. One with no icon, or that never moves, doesn't collapse.
        Assert.Equal(71, Item(cut, "Share").CollapsedWidth);
        Assert.True(Item(cut, "Share").Width > 71);
        Assert.Equal(Item(cut, "Plain").Width, Item(cut, "Plain").CollapsedWidth);
        Assert.Equal(Item(cut, "Stays").Width, Item(cut, "Stays").CollapsedWidth);
        Assert.Equal(71, Item(cut, "Square").Width);
        Assert.Equal(71, Item(cut, "Square").CollapsedWidth);
    }

    [Fact]
    public void The_width_reaches_the_toolbars_container_queries()
    {
        var cut = RenderProbe("collapse");
        var css = cut.Find("ib-toolbar style").TextContent;

        // Labels collapse below the width of all four, three gaps between them and the one the
        // start and end regions leave (8px each).
        var width = new[] { "Share", "Plain", "Square", "Stays" }.Sum(label => Item(cut, label).Width!.Value) + 4 * 8;
        var rem = (width / 16).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains($"@container (width < {rem}rem){{", css);
        Assert.Contains(".ib-toolbar-collapse>.ib-btn-label", css);
    }

    [Fact]
    public void A_plain_one_goes_in_a_toolbar_only_in_a_ToolbarItem()
    {
        Assert.Contains("use ToolbarSplitButton", Assert.Throws<InvalidOperationException>(() => RenderProbe("raw-in-toolbar")).Message);
        Assert.Contains("can't go in a ToolbarButtonGroup", Assert.Throws<InvalidOperationException>(() => RenderProbe("raw-in-group")).Message);
        Assert.NotNull(RenderProbe("in-item").Find(".ib-split-btn"));
    }

    [Fact]
    public void A_toolbar_one_goes_only_in_a_toolbar()
    {
        Assert.Contains("must be directly inside a Toolbar", Assert.Throws<InvalidOperationException>(() => RenderProbe("outside")).Message);
    }
}
