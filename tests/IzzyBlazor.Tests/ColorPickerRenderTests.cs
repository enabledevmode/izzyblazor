using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class ColorPickerRenderTests : BunitContext
{
    public class Model
    {
        [Display(Name = "Brand color")]
        public string? Brand { get; set; } = "#2563EB";

        public string? Empty { get; set; }
    }

    private readonly Model _model = new();
    private readonly EditContext _context;

    public ColorPickerRenderTests() => _context = new EditContext(_model);

    private IRenderedComponent<ColorPicker> Picker(Action<ComponentParameterCollectionBuilder<ColorPicker>>? more = null) =>
        Render<ColorPicker>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, () => _model.Brand);
            p.Add(f => f.Value, _model.Brand);
            p.Add(f => f.ValueChanged, (string? v) => _model.Brand = v);
            more?.Invoke(p);
        });

    [Theory]
    [InlineData("#2563EB", "#2563eb")]
    [InlineData("abc", "#aabbcc")]
    [InlineData(" #ABC ", "#aabbcc")]
    [InlineData("#12345", null)]
    [InlineData("red", null)]
    [InlineData(null, null)]
    public void NormalizeHex(string? input, string? expected) =>
        Assert.Equal(expected, ColorValues.NormalizeHex(input));

    [Fact]
    public void Renders_a_labelled_native_color_input_and_a_hex_box()
    {
        var cut = Picker();

        var color = cut.Find("input[type=color]");
        Assert.Equal("#2563eb", color.GetAttribute("value"));
        Assert.Equal(color.Id, cut.Find("label").GetAttribute("for"));
        Assert.Equal("Brand color", cut.Find("label").TextContent.Trim());
        Assert.Contains("ib-field-color", cut.Find(".ib-field").ClassName);

        var hex = cut.Find("input.ib-color-hex");
        Assert.Equal("#2563eb", hex.GetAttribute("value"));
        Assert.Null(hex.GetAttribute("name"));
        Assert.Equal("Brand color, hex", hex.GetAttribute("aria-label"));
    }

    [Fact]
    public void Picking_binds_the_normalized_value()
    {
        var cut = Picker();

        cut.Find("input[type=color]").Change("#0F766E");

        Assert.Equal("#0f766e", _model.Brand);
    }

    [Fact]
    public void Presets_render_a_datalist_and_swatches()
    {
        var cut = Picker(p => p.Add(f => f.Presets, ["#2563eb", "#0F766E", "teal", "#0f766e"]));

        var list = cut.Find("datalist");
        Assert.Equal(list.Id, cut.Find("input[type=color]").GetAttribute("list"));
        Assert.Equal(new string?[] { "#2563eb", "#0f766e" }, list.QuerySelectorAll("option").Select(o => o.GetAttribute("value")));

        var swatches = cut.FindAll("button.ib-color-preset");
        Assert.Equal(2, swatches.Count);
        Assert.Equal("true", swatches[0].GetAttribute("aria-pressed"));
        Assert.Equal("false", swatches[1].GetAttribute("aria-pressed"));
        Assert.Equal("Preset colors", cut.Find(".ib-color-presets").GetAttribute("aria-label"));
    }

    [Fact]
    public void No_hex_box_when_turned_off()
    {
        var cut = Picker(p => p.Add(f => f.ShowHex, false));

        Assert.Empty(cut.FindAll(".ib-color-hex"));
    }

    [Fact]
    public void Read_only_posts_through_a_hidden_input()
    {
        var cut = Render<ColorPicker>(p => p
            .Add(f => f.ValueExpression, () => _model.Brand)
            .Add(f => f.Value, "#2563eb")
            .Add(f => f.ReadOnly, true)
            .AddUnmatched("name", "brand"));

        var color = cut.Find("input[type=color]");
        Assert.True(color.HasAttribute("disabled"));
        Assert.Null(color.GetAttribute("name"));
        Assert.Equal("#2563eb", cut.Find("input[type=hidden][name=brand]").GetAttribute("value"));
    }

    [Fact]
    public void A_name_attribute_works_outside_an_edit_form()
    {
        var cut = Render<ColorPicker>(p => p
            .Add(f => f.ValueExpression, () => _model.Empty)
            .Add(f => f.Value, null)
            .AddUnmatched("name", "primary"));

        var color = cut.Find("input[type=color]");
        Assert.Equal("primary", color.GetAttribute("name"));
        Assert.Equal("#000000", color.GetAttribute("value"));
    }
}
