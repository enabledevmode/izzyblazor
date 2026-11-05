using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class PasswordRulesTests : BunitContext
{
    public class Model
    {
        public string? Password { get; set; }
    }

    private static readonly PasswordRules Rules = new() { RequiredLength = 12, RequireDigit = true, RequireNonAlphanumeric = true };

    // A static post arrives without the browser's help (no script, or a crafted post): the form
    // validates only a password that meets every rule, and the field tells the browser the rules.
    [Theory]
    [InlineData("correct horse 9", true)]
    [InlineData("correcthorse9é", true)]
    [InlineData("correct horse", false)]
    [InlineData("short 9", false)]
    public void A_new_password_must_meet_its_requirements(string posted, bool valid)
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var model = new Model { Password = posted };
        var context = new EditContext(model);
        var cut = Render<PasswordField>(p => p
            .AddCascadingValue(context)
            .Add(f => f.ValueExpression, () => model.Password)
            .Add(f => f.Value, model.Password)
            .Add(f => f.Requirements, Rules));

        var input = cut.Find("input");
        Assert.Equal("new-password", input.GetAttribute("autocomplete"));
        Assert.Equal("minlength: 12; required: digit; required: special;", input.GetAttribute("passwordrules"));
        Assert.Equal("12", input.GetAttribute("minlength"));
        Assert.Equal(cut.Find(".ib-password-rules").Id, input.GetAttribute("aria-describedby"));
        Assert.Equal(["At least 12 characters", "A number", "A symbol, such as ! or #"], cut.FindAll(".ib-password-rules li").Select(li => li.TextContent.Trim()));

        Assert.Equal(valid, context.Validate());
        Assert.Equal(valid ? [] : ["The Password field doesn't meet every requirement."], context.GetValidationMessages());
    }
}
