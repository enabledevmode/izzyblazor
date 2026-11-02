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
}
