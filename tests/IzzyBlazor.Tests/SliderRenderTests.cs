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
    public void ValueFormat_shapes_the_shown_and_announced_value()
    {
        var cut = SliderFor(() => _model.Discount, p => p.Add(f => f.ValueTooltip, SliderTooltip.Active).Add(f => f.ValueFormat, "{0}%"));

        Assert.Equal("10%", cut.Find(".ib-slider-tip").TextContent);
        Assert.Equal("{0}%", cut.Find("input").GetAttribute("data-ib-format"));
        // On a static page nothing could keep it current without script, so the script sets it:
        // with JavaScript off, screen readers hear the slider's own (always current) value.
        Assert.False(cut.Find("input").HasAttribute("aria-valuetext"));
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
    public void Float_values_read_without_binary_noise()
    {
        var cut = SliderFor(() => _model.Light, p => p.Add(f => f.Min, 0).Add(f => f.Max, 1).Add(f => f.Step, 0.1).Add(f => f.ValueTooltip, SliderTooltip.Active));

        Assert.Equal("0.1", cut.Find(".ib-slider-tip").TextContent);
        Assert.Equal("0.1", cut.Find("input").GetAttribute("value"));
    }
}
