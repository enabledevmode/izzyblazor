using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class PinFieldRenderTests : BunitContext
{
    public class Model
    {
        public string? Code { get; set; }
    }

    // A static post binds the text as sent, which a phone without script or a crafted post can make
    // anything: the field drops spaces and dashes, and the form validates only a whole code.
    [Theory]
    [InlineData("123 456", "123456", true)]
    [InlineData("12-345", "12345", false)]
    [InlineData("12345a", "12345a", false)]
    public void A_posted_code_loses_its_separators_and_must_be_whole(string posted, string bound, bool valid)
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var model = new Model { Code = posted };
        var context = new EditContext(model);
        Render<PinField>(p => p
            .AddCascadingValue(context)
            .Add(f => f.ValueExpression, () => model.Code)
            .Add(f => f.Value, model.Code)
            .Add(f => f.ValueChanged, value => model.Code = value));

        Assert.Equal(bound, model.Code);
        Assert.Equal(valid, context.Validate());
        if (!valid)
        {
            Assert.Contains("The Code field must be 6 digits.", context.GetValidationMessages());
        }
    }
}
