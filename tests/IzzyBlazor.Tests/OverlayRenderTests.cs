using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class OverlayRenderTests : BunitContext
{
    public OverlayRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private static RenderFragment Item(string text, params (string Name, object Value)[] parameters) => builder =>
    {
        builder.OpenComponent<MenuItem>(0);
        builder.AddComponentParameter(1, nameof(MenuItem.ChildContent), (RenderFragment)(b => b.AddContent(0, text)));
        foreach (var (name, value) in parameters)
        {
            builder.AddComponentParameter(2, name, value);
        }

        builder.CloseComponent();
    };

    [Fact]
    public void Menu_wires_trigger_panel_and_anchor()
    {
        var cut = Render<Menu>(p => p
            .Add(m => m.Id, "actions")
            .Add(m => m.Label, "Actions")
            .Add(m => m.Placement, Placement.BottomEnd));

        var root = cut.Find("ib-popover");
        Assert.Equal("anchor-name:--actions", root.GetAttribute("style"));
        Assert.Equal("bottom-end", root.GetAttribute("data-placement"));

        var trigger = cut.Find("button");
        Assert.Equal("actions", trigger.GetAttribute("popovertarget"));
        Assert.Equal("menu", trigger.GetAttribute("aria-haspopup"));

        var panel = cut.Find("[popover]");
        Assert.Equal("auto", panel.GetAttribute("popover"));
        Assert.Equal("menu", panel.GetAttribute("role"));
        Assert.Equal("actions-trigger", panel.GetAttribute("aria-labelledby"));
        Assert.Equal("ib-pop ib-menu ib-place-bottom-end", panel.ClassName);
        Assert.StartsWith("position-anchor:--actions", panel.GetAttribute("style"));
    }

    [Fact]
    public void Menu_items_are_links_buttons_or_form_posts()
    {
        var cut = Render<Menu>(p => p
            .Add(m => m.Id, "m")
            .Add(m => m.Label, "Menu")
            .Add(m => m.ChildContent, (RenderFragment)(b =>
            {
                b.AddContent(0, Item("Settings", (nameof(MenuItem.Href), "/settings")));
                b.AddContent(1, Item("Copy", (nameof(MenuItem.Shortcut), "Ctrl+C")));
                b.AddContent(2, Item("Delete",
                    (nameof(MenuItem.Type), ButtonType.Submit),
                    (nameof(MenuItem.FormAction), "/delete"),
                    (nameof(MenuItem.Accent), Accent.Danger)));
            })));

        var items = cut.FindAll("[role=menuitem]");
        Assert.Equal("A", items[0].TagName);
        Assert.Equal("/settings", items[0].GetAttribute("href"));

        Assert.Equal("button", items[1].GetAttribute("type"));
        Assert.Equal("m", items[1].GetAttribute("popovertarget"));
        Assert.Equal("hide", items[1].GetAttribute("popovertargetaction"));
        Assert.Equal("Ctrl+C", items[1].QuerySelector(".ib-menu-shortcut")!.TextContent);

        Assert.Equal("submit", items[2].GetAttribute("type"));
        Assert.Equal("/delete", items[2].GetAttribute("formaction"));
        Assert.Null(items[2].GetAttribute("popovertarget"));
        Assert.Contains("ib-menu-item-danger", items[2].ClassName);
    }

    [Fact]
    public void Menu_custom_trigger_receives_wiring_attributes()
    {
        var cut = Render<Menu>(p => p
            .Add(m => m.Id, "more")
            .Add(m => m.Trigger, (RenderFragment<IReadOnlyDictionary<string, object>>)(attributes => b =>
            {
                b.OpenComponent<IconButton>(0);
                b.AddComponentParameter(1, nameof(IconButton.Icon), Lucide.Ellipsis);
                b.AddComponentParameter(2, nameof(IconButton.Label), "More");
                b.AddMultipleAttributes(3, attributes);
                b.CloseComponent();
            })));

        var trigger = cut.Find("button[aria-label=More]");
        Assert.Equal("more", trigger.GetAttribute("popovertarget"));
        Assert.Equal("more-trigger", trigger.Id);
    }

    [Fact]
    public void Open_menu_sets_data_open_for_the_element()
    {
        var cut = Render<Menu>(p => p.Add(m => m.Label, "Menu").Add(m => m.Open, true));

        Assert.True(cut.Find("ib-popover").HasAttribute("data-open"));
    }

    [Fact]
    public void Popover_toggle_event_reports_state()
    {
        bool? reported = null;
        var cut = Render<Popover>(p => p
            .Add(x => x.Id, "filters")
            .Add(x => x.Label, "Filters")
            .Add(x => x.OpenChanged, (bool open) => reported = open));

        cut.Find("[popover]").TriggerEvent("onibtoggle", new IzzyToggleEventArgs { Id = "filters", Open = true });

        Assert.True(reported);
        Assert.True(cut.Find("ib-popover").HasAttribute("data-open"));
    }

    [Fact]
    public void Manual_popover_gets_a_close_button()
    {
        var cut = Render<Popover>(p => p
            .Add(x => x.Id, "note")
            .Add(x => x.Label, "Note")
            .Add(x => x.Title, "Pinned")
            .Add(x => x.Mode, PopoverMode.Manual));

        Assert.Equal("manual", cut.Find("[popover]").GetAttribute("popover"));
        Assert.Equal("note-title", cut.Find("[popover]").GetAttribute("aria-labelledby"));
        var close = cut.Find(".ib-popover-close");
        Assert.Equal("note", close.GetAttribute("popovertarget"));
        Assert.Equal("hide", close.GetAttribute("popovertargetaction"));
    }

    [Fact]
    public void Tooltip_renders_css_fallback_with_role()
    {
        var cut = Render<Tooltip>(p => p
            .Add(t => t.Text, "Copy")
            .Add(t => t.Placement, Placement.Bottom)
            .Add(t => t.Delay, 150)
            .AddChildContent("<button>Copy</button>"));

        var tip = cut.Find("[role=tooltip]");
        Assert.Equal("ib-tooltip ib-tooltip-bottom", tip.ClassName);
        Assert.Contains("--ib-tooltip-delay:150ms", tip.GetAttribute("style"));
        Assert.Equal("150", cut.Find("ib-tooltip").GetAttribute("data-delay"));
    }

    [Fact]
    public void Dialog_renders_closed_with_labelled_title_and_close_button()
    {
        var cut = Render<Dialog>(p => p
            .Add(d => d.Id, "confirm")
            .Add(d => d.Title, "Delete?")
            .Add(d => d.Size, DialogSize.Sm)
            .AddChildContent("Sure?"));

        var dialog = cut.Find("dialog");
        Assert.False(dialog.HasAttribute("open"));
        Assert.Equal("ib-dialog ib-dialog-sm ib-dialog-center", dialog.ClassName);
        Assert.Equal("confirm-title", dialog.GetAttribute("aria-labelledby"));
        Assert.Equal("any", dialog.GetAttribute("closedby"));
        Assert.Equal("Delete?", cut.Find("#confirm-title").TextContent);

        // Close buttons submit a method="dialog" form, which closes it natively with or without script.
        Assert.Equal("dialog", cut.Find("form#confirm-close").GetAttribute("method"));
        var close = cut.Find(".ib-dialog-close");
        Assert.Equal("submit", close.GetAttribute("type"));
        Assert.Equal("confirm-close", close.GetAttribute("form"));
        Assert.Equal("Close", close.GetAttribute("aria-label"));
    }

    [Fact]
    public void Dialog_opens_from_the_query_string()
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/projects?dialog=confirm");

        var open = Render<Dialog>(p => p.Add(d => d.Id, "confirm").Add(d => d.Title, "Delete?"));
        var other = Render<Dialog>(p => p.Add(d => d.Id, "invite").Add(d => d.Title, "Invite"));

        Assert.True(open.Find("dialog").HasAttribute("open"));
        Assert.True(open.Find("ib-dialog").HasAttribute("data-open"));
        Assert.False(other.Find("dialog").HasAttribute("open"));
    }

    [Fact]
    public void Dialog_without_backdrop_close_waits_for_a_close_request()
    {
        var cut = Render<Dialog>(p => p.Add(d => d.Id, "d").Add(d => d.CloseOnBackdrop, false));

        Assert.Equal("closerequest", cut.Find("dialog").GetAttribute("closedby"));
        Assert.False(cut.Find("ib-dialog").HasAttribute("data-close-on-backdrop"));
    }

    [Fact]
    public void DialogTrigger_opens_with_a_command_and_DialogClose_closes_with_the_dialog_form()
    {
        var trigger = Render<DialogTrigger>(p => p.Add(t => t.For, "invite").AddChildContent("Invite")).Find("button");
        Assert.Equal("invite", trigger.GetAttribute("commandfor"));
        Assert.Equal("show-modal", trigger.GetAttribute("command"));

        var dialog = Render<Dialog>(p => p
            .Add(d => d.Id, "invite")
            .Add(d => d.Footer, (RenderFragment)(b =>
            {
                b.OpenComponent<DialogClose>(0);
                b.AddComponentParameter(1, nameof(DialogClose.ChildContent), (RenderFragment)(c => c.AddContent(0, "Cancel")));
                b.CloseComponent();
            })));
        var close = dialog.Find(".ib-dialog-footer button");
        Assert.Equal("submit", close.GetAttribute("type"));
        Assert.Equal("invite-close", close.GetAttribute("form"));
        Assert.Contains("ib-btn-outline", close.ClassName);
    }

    [Fact]
    public void DialogClose_outside_a_dialog_needs_For()
    {
        Assert.Throws<InvalidOperationException>(() => Render<DialogClose>());
    }

    [Fact]
    public void Dialog_inside_a_form_closes_with_commands_and_renders_no_form()
    {
        var dialog = Render<Dialog>(p => p
            .Add(d => d.Id, "d")
            .Add(d => d.Title, "Sure?")
            .Add(d => d.InsideForm, true)
            .Add(d => d.Footer, (RenderFragment)(b =>
            {
                b.OpenComponent<DialogClose>(0);
                b.AddComponentParameter(1, nameof(DialogClose.ChildContent), (RenderFragment)(c => c.AddContent(0, "Cancel")));
                b.CloseComponent();
            })));

        Assert.Empty(dialog.FindAll("form"));
        foreach (var close in new[] { dialog.Find(".ib-dialog-close"), dialog.Find(".ib-dialog-footer button") })
        {
            Assert.Equal("button", close.GetAttribute("type"));
            Assert.Null(close.GetAttribute("form"));
            Assert.Equal("d", close.GetAttribute("commandfor"));
            Assert.Equal("close", close.GetAttribute("command"));
        }
    }

    [Fact]
    public void ConfirmButton_opens_an_alert_dialog_whose_confirm_submits_with_name_and_value()
    {
        var cut = Render<ConfirmButton>(p => p
            .Add(c => c.Id, "del")
            .Add(c => c.Title, "Delete this project?")
            .Add(c => c.Message, "It can't be undone.")
            .Add(c => c.ConfirmText, "Delete")
            .Add(c => c.Accent, Accent.Danger)
            .Add(c => c.Name, "action")
            .Add(c => c.Value, "delete")
            .AddChildContent("Delete"));

        var trigger = cut.Find("button#del");
        Assert.Equal("button", trigger.GetAttribute("type"));
        Assert.Equal("del-dialog", trigger.GetAttribute("commandfor"));
        Assert.Equal("show-modal", trigger.GetAttribute("command"));
        Assert.Contains("ib-btn-danger", trigger.ClassName);

        var dialog = cut.Find("dialog#del-dialog");
        Assert.Equal("alertdialog", dialog.GetAttribute("role"));
        Assert.Equal("closerequest", dialog.GetAttribute("closedby"));
        Assert.Equal("del-message", dialog.GetAttribute("aria-describedby"));
        Assert.Equal("It can't be undone.", cut.Find("#del-message").TextContent.Trim());
        Assert.Empty(cut.FindAll("form"));
        Assert.Empty(cut.FindAll(".ib-dialog-close"));

        var buttons = cut.FindAll(".ib-dialog-footer button");
        Assert.Equal("Cancel", buttons[0].TextContent);
        Assert.True(buttons[0].HasAttribute("autofocus"));
        Assert.Equal("button", buttons[0].GetAttribute("type"));
        Assert.Equal("submit", buttons[1].GetAttribute("type"));
        Assert.Equal("action", buttons[1].GetAttribute("name"));
        Assert.Equal("delete", buttons[1].GetAttribute("value"));
        Assert.Equal("Delete", buttons[1].TextContent);
        Assert.Contains("ib-btn-danger", buttons[1].ClassName);
    }

    [Fact]
    public void ConfirmButton_confirms_with_a_link_or_with_C_sharp_on_interactive_pages()
    {
        var link = Render<ConfirmButton>(p => p.Add(c => c.Title, "Leave?").Add(c => c.Href, "/logout").AddChildContent("Sign out"));
        Assert.Equal("/logout", link.Find(".ib-dialog-footer a").GetAttribute("href"));

        // Static: OnConfirm can't run, so confirming submits the form it's in.
        var staticPage = Render<ConfirmButton>(p => p.Add(c => c.Title, "Reset?").Add(c => c.OnConfirm, () => { }).AddChildContent("Reset"));
        Assert.Equal("submit", staticPage.FindAll(".ib-dialog-footer button")[1].GetAttribute("type"));

        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var confirmed = 0;
        var live = Render<ConfirmButton>(p => p.Add(c => c.Id, "r").Add(c => c.Title, "Reset?").Add(c => c.OnConfirm, () => confirmed++).AddChildContent("Reset"));
        var confirm = live.FindAll(".ib-dialog-footer button")[1];
        Assert.Equal("button", confirm.GetAttribute("type"));
        Assert.Equal("r-dialog", confirm.GetAttribute("commandfor"));
        Assert.Equal("close", confirm.GetAttribute("command"));
        confirm.Click();
        Assert.Equal(1, confirmed);
    }
}
