using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

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

    private sealed class Picks
    {
        [Required] public string? Country { get; set; }
        [Required] public string[]? Tags { get; set; }
        public string[]? Optional { get; set; }
        [Required] public string? Size { get; set; }
        public string? Color { get; set; }
        public string? Notes { get; set; }
    }

    [Fact]
    public void A_required_Combobox_or_MultiSelect_says_so_on_its_text_input()
    {
        var model = new Picks();
        var context = new EditContext(model);
        var combobox = Render<Combobox<string?>>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Country).Add(c => c.ValueExpression, () => model.Country).Add(c => c.Items, ["a", "b"]));
        var multi = Render<MultiSelect<string[]?, string>>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Tags).Add(c => c.ValueExpression, () => model.Tags).Add(c => c.Items, ["a", "b"]));
        var optional = Render<MultiSelect<string[]?, string>>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Optional).Add(c => c.ValueExpression, () => model.Optional).Add(c => c.Items, ["a", "b"]));

        Assert.Equal("true", combobox.Find("input[role=combobox]").GetAttribute("aria-required"));
        Assert.Equal("true", multi.Find("input[role=combobox]").GetAttribute("aria-required"));
        Assert.Null(optional.Find("input[role=combobox]").GetAttribute("aria-required"));
    }

    [Fact]
    public void A_MultiSelects_chosen_options_describe_its_input()
    {
        var model = new Picks { Tags = ["a", "c"] };
        var context = new EditContext(model);
        var cut = Render<MultiSelect<string[]?, string>>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Tags).Add(c => c.ValueExpression, () => model.Tags).Add(c => c.Items, ["a", "b", "c"])
            .Add(c => c.Hint, "Up to three"));

        var input = cut.Find("input[role=combobox]");
        var chosen = cut.Find(".ib-multiselect-chosen");
        Assert.True(chosen.HasAttribute("hidden"));
        Assert.Equal("Chosen: a, c.", chosen.TextContent);
        Assert.Equal($"{chosen.Id} {input.Id!.Replace("-input", "-hint")}", input.GetAttribute("aria-describedby"));
        Assert.Equal("Chosen: {0}.", cut.Find("ib-combobox").GetAttribute("data-chosen-label"));
    }

    [Fact]
    public void A_CheckboxGroup_description_isnt_part_of_the_name()
    {
        var model = new Picks();
        var context = new EditContext(model);
        var cut = Render<CheckboxGroup<string[]?, string>>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Tags).Add(c => c.ValueExpression, () => model.Tags).Add(c => c.Items, ["a", "b"])
            .Add(c => c.ItemDescription, item => item == "a" ? "First" : null));

        var (first, second) = (cut.FindAll("input[type=checkbox]")[0], cut.FindAll("input[type=checkbox]")[1]);
        Assert.Equal("a", cut.Find($"#{first.GetAttribute("aria-labelledby")}").TextContent);
        Assert.Equal("First", cut.Find($"#{first.GetAttribute("aria-describedby")}").TextContent);
        Assert.Null(second.GetAttribute("aria-labelledby"));
        Assert.Null(second.GetAttribute("aria-describedby"));
    }

    [Fact]
    public void NavMenu_labels_and_dividers_are_plain_or_hidden_list_items()
    {
        var label = Render<NavLabel>(p => p.AddChildContent("Workspace")).Find("li");
        var divider = Render<NavDivider>().Find("li");

        Assert.Null(label.GetAttribute("role"));
        Assert.Null(divider.GetAttribute("role"));
        Assert.Equal("true", divider.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void A_tab_isnt_a_label_element()
    {
        RenderFragment two = b =>
        {
            b.OpenComponent<Tab>(0);
            b.AddAttribute(1, nameof(Tab.Label), "One");
            b.CloseComponent();
            b.OpenComponent<Tab>(2);
            b.AddAttribute(3, nameof(Tab.Label), "Two");
            b.CloseComponent();
        };

        Assert.Null(Render<Tabs>(p => p.Add(t => t.ChildContent, two)).Find(".ib-tabs-list").GetAttribute("aria-label"));

        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Account").Add(t => t.ChildContent, two));
        var tab = cut.Find(".ib-tab");
        var input = tab.QuerySelector("input")!;
        Assert.Equal("SPAN", tab.TagName);
        Assert.Equal("Account", cut.Find(".ib-tabs-list").GetAttribute("aria-label"));
        Assert.Equal(input.Id, tab.QuerySelector("label.ib-tab-label")!.GetAttribute("for"));
        Assert.Equal("One", tab.QuerySelector("label")!.TextContent.Trim());
    }

    [Fact]
    public void A_filter_select_date_or_checkbox_needs_a_label()
    {
        Assert.Throws<InvalidOperationException>(() => Render<FilterSelect>(p => p.Add(f => f.Name, "status")));
        Assert.Throws<InvalidOperationException>(() => Render<FilterDate>(p => p.Add(f => f.Name, "from")));
        Assert.Throws<InvalidOperationException>(() => Render<FilterCheckbox>(p => p.Add(f => f.Name, "mine")));

        var search = Render<FilterSearch>();
        Assert.Equal("Search", search.Find("label").TextContent);
    }

    [Fact]
    public void ColorPicker_presets_take_names_and_read_only_shows_the_hex_box()
    {
        var model = new Picks();
        var context = new EditContext(model);
        var cut = Render<ColorPicker>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Color).Add(c => c.ValueExpression, () => model.Color)
            .Add(c => c.Presets, ["#2563EB", "#0f766e"])
            .Add(c => c.PresetText, hex => hex == "#2563EB" ? "Ocean" : null)
            .Add(c => c.ShowHex, false)
            .Add(c => c.ReadOnly, true));

        var presets = cut.FindAll(".ib-color-preset");
        Assert.Equal(("Ocean", "Ocean"), (presets[0].GetAttribute("aria-label"), presets[0].GetAttribute("title")));
        Assert.Equal("#0f766e", presets[1].GetAttribute("aria-label"));

        var hex = cut.Find(".ib-color-hex");
        Assert.True(hex.HasAttribute("readonly"));
        Assert.False(hex.HasAttribute("disabled"));
        Assert.Contains("ib-color-readonly", cut.Find("ib-color-picker").ClassName);
    }

    [Fact]
    public void A_required_CheckboxGroup_says_so_in_its_legend()
    {
        var model = new Picks();
        var context = new EditContext(model);
        var required = Render<CheckboxGroup<string[]?, string>>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Tags).Add(c => c.ValueExpression, () => model.Tags).Add(c => c.Items, ["a", "b"]));
        var optional = Render<CheckboxGroup<string[]?, string>>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Optional).Add(c => c.ValueExpression, () => model.Optional).Add(c => c.Items, ["a", "b"]));
        var radios = Render<RadioGroup<string?>>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Size).Add(c => c.ValueExpression, () => model.Size).Add(c => c.Items, ["S", "M"]));

        Assert.Equal("(required)", required.Find("legend .ib-visually-hidden").TextContent);
        Assert.Empty(optional.FindAll("legend .ib-visually-hidden"));
        // Radios are marked required themselves.
        Assert.Empty(radios.FindAll("legend .ib-visually-hidden"));
    }

    [Fact]
    public void An_invalid_group_takes_focus_after_a_post()
    {
        var model = new Picks { Size = "M" };
        var context = new EditContext(model);
        var messages = new ValidationMessageStore(context);
        messages.Add(context.Field(nameof(Picks.Tags)), "Pick one.");
        messages.Add(context.Field(nameof(Picks.Size)), "Too big.");
        var boxes = Render<CheckboxGroup<string[]?, string>>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Tags).Add(c => c.ValueExpression, () => model.Tags).Add(c => c.Items, ["a", "b"]));
        var radios = Render<RadioGroup<string?>>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Size).Add(c => c.ValueExpression, () => model.Size).Add(c => c.Items, ["S", "M"]));

        var first = boxes.FindAll("input[type=checkbox]");
        Assert.True(first[0].HasAttribute("autofocus") && first[0].HasAttribute("data-ib-autofocus"));
        Assert.False(first[1].HasAttribute("autofocus"));
        // The checked radio, where Tab lands.
        var checkedRadio = radios.Find("input[value=M]");
        Assert.True(checkedRadio.HasAttribute("autofocus") && checkedRadio.HasAttribute("data-ib-autofocus"));
        Assert.False(radios.Find("input[value=S]").HasAttribute("autofocus"));

        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var live = Render<CheckboxGroup<string[]?, string>>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Tags).Add(c => c.ValueExpression, () => model.Tags).Add(c => c.Items, ["a", "b"]));
        Assert.Empty(live.FindAll("[autofocus], [data-ib-autofocus]"));
    }

    [Fact]
    public void FieldError_takes_an_id_to_link()
    {
        var model = new Picks();
        var context = new EditContext(model);
        new ValidationMessageStore(context).Add(context.Field(nameof(Picks.Size)), "Pick a size.");
        var cut = Render<FieldError<string?>>(p => p.AddCascadingValue(context).Add(f => f.For, () => model.Size).Add(f => f.Id, "size-error"));

        Assert.Equal("size-error", cut.Find(".ib-field-error").Id);
    }

    [Fact]
    public void A_TextArea_count_has_a_live_number_for_the_script_to_link()
    {
        var model = new Picks { Notes = "Hello" };
        var context = new EditContext(model);
        var cut = Render<TextArea>(p => p.AddCascadingValue(context)
            .Add(c => c.Value, model.Notes).Add(c => c.ValueExpression, () => model.Notes)
            .Add(c => c.MaxLength, 200).Add(c => c.ShowCount, true).Add(c => c.Hint, "Short."));

        var area = cut.Find("textarea");
        var count = cut.Find(".ib-field-count");
        // The script links it: with JavaScript off its number would be stale.
        Assert.Equal($"{area.Id}-hint", area.GetAttribute("aria-describedby"));
        Assert.Equal($"{area.Id}-count", count.Id);
        Assert.Equal("5", cut.Find(".ib-field-count-current").TextContent);
        Assert.Equal("5 / 200", count.TextContent);
    }
}
