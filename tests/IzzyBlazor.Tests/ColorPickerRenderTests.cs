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
    [InlineData("#12345", null)]
    public void NormalizeHex(string? input, string? expected) =>
        Assert.Equal(expected, ColorValues.NormalizeHex(input));

    [Fact]
    public void Picking_binds_the_normalized_value()
    {
        var cut = Picker();

        cut.Find("input[type=color]").Change("#0F766E");

        Assert.Equal("#0f766e", _model.Brand);
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

        // Disabled as well: nothing posts.
        var disabled = Render<ColorPicker>(p => p
            .Add(f => f.ValueExpression, () => _model.Brand)
            .Add(f => f.Value, "#2563eb")
            .Add(f => f.ReadOnly, true)
            .Add(f => f.Disabled, true)
            .AddUnmatched("name", "brand"));
        Assert.Empty(disabled.FindAll("input[type=hidden]"));
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
