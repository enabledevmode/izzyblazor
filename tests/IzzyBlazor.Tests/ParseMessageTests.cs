using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class ParseMessageTests : BunitContext
{
    [Fact]
    public void A_value_that_can_t_be_read_takes_the_app_s_text()
    {
        Services.AddSingleton(new IzzyText { ValueInvalid = "{0} ist ungültig.", NumberInvalid = "{0} muss eine Zahl sein." });
        var model = new NumberModel();
        var context = new EditContext(model);
        var select = Render<Select<int>>(p => p.AddCascadingValue(context).Add(t => t.ValueExpression, () => model.Size).Add(t => t.Items, new[] { 1, 2 }));
        var number = Render<NumberField<int>>(p => p.AddCascadingValue(context).Add(t => t.ValueExpression, () => model.Count));

        select.Find("select").Change("huge");
        number.Find("input").Change("many");

        Assert.Contains("Size ist ungültig.", context.GetValidationMessages());
        Assert.Contains("Count muss eine Zahl sein.", context.GetValidationMessages());
    }

    public class NumberModel
    {
        public int Size { get; set; }

        public int Count { get; set; }
    }
}
