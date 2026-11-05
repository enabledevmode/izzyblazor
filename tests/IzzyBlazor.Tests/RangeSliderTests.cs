using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class RangeSliderTests : BunitContext
{
    public class Model
    {
        public NumberRange<decimal>? Price { get; set; }
    }

    public RangeSliderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    // A static post binds the model before the field renders: crossed ends (no script) are put in
    // order, and an end outside the scale (a crafted post) is turned away.
    [Theory]
    [InlineData(300, 100, 100, 300, null)]
    [InlineData(-50, 100, -50, 100, "Price must be between $0 and $400.")]
    [InlineData(100, 500, 100, 500, "Price must be between $0 and $400.")]
    public void A_posted_range_is_put_in_order_and_checked(decimal start, decimal end, decimal boundStart, decimal boundEnd, string? error)
    {
        var model = new Model { Price = new(start, end) };
        var context = new EditContext(model);
        Render<RangeSlider<decimal>>(p => p
            .AddCascadingValue(context)
            .Add(f => f.ValueExpression, () => model.Price)
            .Add(f => f.Value, model.Price)
            .Add(f => f.ValueChanged, v => model.Price = v)
            .Add(f => f.Max, 400)
            .Add(f => f.ValueFormat, "${0}"));

        Assert.Equal(new NumberRange<decimal>(boundStart, boundEnd), model.Price);
        Assert.Equal(error is null, context.Validate());
        Assert.Equal(error is null ? [] : [error], context.GetValidationMessages());
    }
}
