using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class SliderRenderTests : BunitContext
{
    public class Model
    {
        public int Volume { get; set; } = 25;

        [Range(0, 50)]
        public int Discount { get; set; } = 10;

        public double Opacity { get; set; } = 0.5;

        public int? Unset { get; set; }

        [Range(1, 10)]
        public int? Rating { get; set; }

        public float Light { get; set; } = 0.1f;

        public DateTime When { get; set; }
    }

    private readonly Model _model = new();
    private readonly EditContext _context;

    public SliderRenderTests()
    {
        _context = new EditContext(_model);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    private IRenderedComponent<Slider<T>> SliderFor<T>(Expression<Func<T>> field, Action<ComponentParameterCollectionBuilder<Slider<T>>>? more = null) =>
        Render<Slider<T>>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, field);
            p.Add(f => f.Value, field.Compile()());
            more?.Invoke(p);
        });

    [Fact]
    public void Renders_a_range_input_defaulting_to_0_to_100()
    {
        var cut = SliderFor(() => _model.Volume);
        var input = cut.Find("input");

        Assert.Equal("range", input.GetAttribute("type"));
        Assert.Equal("0", input.GetAttribute("min"));
        Assert.Equal("100", input.GetAttribute("max"));
        Assert.Equal("25", input.GetAttribute("value"));
        Assert.EndsWith("Volume", input.GetAttribute("name"));
        Assert.Equal("--ib-slider-fill: 25", cut.Find("ib-field-control").GetAttribute("style"));
    }

    [Fact]
    public void Range_comes_from_annotations_and_fill_follows_it()
    {
        var cut = SliderFor(() => _model.Discount);

        Assert.Equal("50", cut.Find("input").GetAttribute("max"));
        Assert.Equal("--ib-slider-fill: 20", cut.Find("ib-field-control").GetAttribute("style"));
    }

    [Fact]
    public void ValueFormat_shapes_the_shown_and_announced_value()
    {
        var cut = SliderFor(() => _model.Discount, p => p.Add(f => f.ShowValue, true).Add(f => f.ValueFormat, "{0}%"));

        Assert.Equal("10%", cut.Find("output").TextContent);
        Assert.Equal("{0}%", cut.Find("input").GetAttribute("data-ib-format"));
        // On a static page nothing could keep it current without script, so the script sets it:
        // with JavaScript off, screen readers hear the slider's own (always current) value.
        Assert.False(cut.Find("input").HasAttribute("aria-valuetext"));
    }

    [Fact]
    public void No_value_output_unless_asked()
    {
        Assert.Empty(SliderFor(() => _model.Volume).FindAll("output"));
    }

    [Fact]
    public void Fractional_values_use_invariant_formatting()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var input = SliderFor(() => _model.Opacity, p => p.Add(f => f.Min, 0).Add(f => f.Max, 1).Add(f => f.Step, 0.05)).Find("input");

            Assert.Equal("0.5", input.GetAttribute("value"));
            Assert.Equal("0.05", input.GetAttribute("step"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Empty_value_sits_at_the_midpoint()
    {
        var cut = SliderFor(() => _model.Unset, p => p.Add(f => f.ShowValue, true));

        Assert.Equal("50", cut.Find("output").TextContent);
        Assert.Equal("--ib-slider-fill: 50", cut.Find("ib-field-control").GetAttribute("style"));
    }

    [Fact]
    public void Empty_value_snaps_to_the_step_like_the_browser()
    {
        var cut = SliderFor(() => _model.Rating, p => p.Add(f => f.ShowValue, true));

        Assert.Equal("6", cut.Find("output").TextContent);
    }

    [Fact]
    public void Float_values_read_without_binary_noise()
    {
        var cut = SliderFor(() => _model.Light, p => p.Add(f => f.Min, 0).Add(f => f.Max, 1).Add(f => f.Step, 0.1).Add(f => f.ShowValue, true));

        Assert.Equal("0.1", cut.Find("output").TextContent);
        Assert.Equal("0.1", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Read_only_says_so_and_posts_its_value_from_a_hidden_input()
    {
        var cut = SliderFor(() => _model.Volume, p => p.Add(f => f.ReadOnly, true));
        var range = cut.Find("input[type=range]");
        var hidden = cut.Find("input[type=hidden]");

        // A range can't be read-only in HTML: it arrives disabled (the script enables it), says
        // it's read-only, and sits outside the form (form=""), so only the hidden input posts.
        Assert.True(range.HasAttribute("disabled"));
        Assert.Equal("true", range.GetAttribute("aria-readonly"));
        Assert.Equal("", range.GetAttribute("form"));
        Assert.Equal("25", hidden.GetAttribute("value"));
        Assert.EndsWith("Volume", hidden.GetAttribute("name"));
    }

    [Fact]
    public void Rejects_non_number_types()
    {
        Assert.ThrowsAny<InvalidOperationException>(() => SliderFor(() => _model.When));
    }
}
