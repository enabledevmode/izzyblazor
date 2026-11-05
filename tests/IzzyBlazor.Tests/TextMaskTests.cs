using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class TextMaskTests : BunitContext
{
    public class Model
    {
        public string? Phone { get; set; }
    }

    // Whatever arrives (typed, pasted, posted with no script) comes out in the mask's form. mask.ts
    // runs the same algorithm in the browser: a case that changes here changes there.
    [Theory]
    [InlineData("(000) 000-0000", "5551234567", "(555) 123-4567", "5551234567", true)]
    [InlineData("(000) 000-0000", "555.123.4567", "(555) 123-4567", "5551234567", true)]
    [InlineData("(000) 000-0000", "(555) 123-4567", "(555) 123-4567", "5551234567", true)]
    [InlineData("(000) 000-0000", "5551", "(555) 1", "5551", false)]
    [InlineData("(000) 000-0000", "555", "(555", "555", false)]
    [InlineData("(000) 000-0000", "555123456789", "(555) 123-4567", "5551234567", true)]
    [InlineData("+1 (000) 000-0000", "15551234567", "+1 (555) 123-4567", "5551234567", true)]
    [InlineData(">L0L 0L0", "k1a0b1", "K1A 0B1", "K1A0B1", true)]
    [InlineData("00000-9999", "12345", "12345", "12345", true)]
    [InlineData("00000-9999", "123456789", "12345-6789", "123456789", true)]
    [InlineData("0000 USD", "1234", "1234 USD", "1234", true)]
    [InlineData("LLL-0000", "ab1234", "ab", "ab", false)]
    public void Text_is_fitted_to_the_mask(string mask, string text, string formatted, string raw, bool complete)
    {
        Assert.Equal(new TextMask.Fit(formatted, raw, complete), new TextMask(mask).Apply(text));
    }

    [Fact]
    public void A_rule_adds_a_mask_character()
    {
        var mask = new TextMask("XXX 000", new Dictionary<char, string> { ['X'] = "[A-HJ-NP-Z]" });
        Assert.Equal("ABC 123", mask.Apply("AIBOC123").Text);
        Assert.Throws<ArgumentException>(() => new TextMask("X", new Dictionary<char, string> { ['X'] = "A|B" }));
    }

    // A static post of an unformatted number binds formatted (or raw), and an incomplete one is invalid.
    [Theory]
    [InlineData("555.123.4567", false, "(555) 123-4567", true)]
    [InlineData("555.123.4567", true, "5551234567", true)]
    [InlineData("555 1234", false, "(555) 123-4", false)]
    public void A_posted_value_binds_in_the_mask_form(string posted, bool rawValue, string bound, bool valid)
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var model = new Model();
        var context = new EditContext(model);
        var cut = Render<TextField>(p => p
            .AddCascadingValue(context)
            .Add(f => f.ValueExpression, () => model.Phone)
            .Add(f => f.Value, model.Phone)
            .Add(f => f.ValueChanged, v => model.Phone = v)
            .Add(f => f.Mask, "(000) 000-0000")
            .Add(f => f.RawValue, rawValue));

        var input = cut.Find("input");
        Assert.Equal("numeric", input.GetAttribute("inputmode"));
        input.Change(posted);

        Assert.Equal(bound, model.Phone);
        Assert.Equal(valid, context.Validate());
        Assert.Equal(valid ? [] : ["The Phone field is incomplete."], context.GetValidationMessages());
    }
}
