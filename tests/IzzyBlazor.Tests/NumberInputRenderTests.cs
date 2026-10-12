using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class NumberInputRenderTests : BunitContext
{
    public class Model
    {
        [Range(1, 99)]
        public int Quantity { get; set; } = 3;

        [Range(typeof(decimal), "0", "10000")]
        public decimal? Price { get; set; } = 1.5m;

        public double Ratio { get; set; }

        public string? Name { get; set; }
    }

    private readonly Model _model = new();
    private readonly EditContext _context;

    public NumberInputRenderTests()
    {
        _context = new EditContext(_model);
    }

    private IRenderedComponent<NumberInput<T>> NumberFor<T>(Expression<Func<T>> field, Action<ComponentParameterCollectionBuilder<NumberInput<T>>>? more = null) =>
        Render<NumberInput<T>>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, field);
            p.Add(f => f.Value, field.Compile()());
            more?.Invoke(p);
        });

    [Fact]
    public void Renders_a_number_input_with_range_from_annotations()
    {
        var input = NumberFor(() => _model.Quantity).Find("input");

        Assert.Equal("number", input.GetAttribute("type"));
        Assert.Equal("1", input.GetAttribute("min"));
        Assert.Equal("99", input.GetAttribute("max"));
        Assert.False(input.HasAttribute("step"));
        Assert.Equal("3", input.GetAttribute("value"));
        Assert.EndsWith("Quantity", input.GetAttribute("name"));
    }

    [Fact]
    public void Parameters_override_the_range()
    {
        var input = NumberFor(() => _model.Quantity, p => p.Add(f => f.Min, 0).Add(f => f.Max, 10).Add(f => f.Step, 2)).Find("input");

        Assert.Equal("0", input.GetAttribute("min"));
        Assert.Equal("10", input.GetAttribute("max"));
        Assert.Equal("2", input.GetAttribute("step"));
    }

    [Fact]
    public void Decimal_types_allow_any_step_unless_given_one()
    {
        Assert.Equal("any", NumberFor(() => _model.Price).Find("input").GetAttribute("step"));
        Assert.Equal("0.01", NumberFor(() => _model.Price, p => p.Add(f => f.Step, 0.01)).Find("input").GetAttribute("step"));
    }

    [Fact]
    public void Values_use_invariant_formatting_in_any_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            Assert.Equal("1.5", NumberFor(() => _model.Price).Find("input").GetAttribute("value"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Steppers_render_hidden_with_names_and_can_be_turned_off()
    {
        var cut = NumberFor(() => _model.Quantity);
        var steppers = cut.Find("[data-ib-steppers]");
        var buttons = steppers.QuerySelectorAll("button");

        Assert.True(steppers.HasAttribute("hidden"));
        Assert.Equal(new[] { "Increase", "Decrease" }, buttons.Select(b => b.GetAttribute("aria-label")).ToArray());
        Assert.All(buttons, b => Assert.Equal("-1", b.GetAttribute("tabindex")));

        Assert.Empty(NumberFor(() => _model.Quantity, p => p.Add(f => f.Steppers, StepperLayout.None)).FindAll("[data-ib-steppers]"));
    }

    [Fact]
    public void Split_steppers_put_decrease_first_and_increase_last()
    {
        var cut = NumberFor(() => _model.Quantity, p => p.Add(f => f.Steppers, StepperLayout.Split));
        var steps = cut.FindAll("[data-ib-steppers] button").Select(b => b.GetAttribute("data-ib-step")).ToArray();

        Assert.Equal(new[] { "down", "up" }, steps);
        Assert.Contains("ib-has-split-steppers", cut.Find("ib-field-control").ClassList);
        Assert.True(cut.Find(".ib-field-steppers-split").HasAttribute("hidden"));
    }

    [Fact]
    public void Disabled_field_disables_its_steppers()
    {
        var buttons = NumberFor(() => _model.Quantity, p => p.Add(f => f.Disabled, true)).FindAll(".ib-field-step");

        Assert.All(buttons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Takes_addons()
    {
        var cut = NumberFor(() => _model.Price, p => p.Add(f => f.StartAddon, "$"));

        Assert.Equal("$", cut.Find(".ib-field-group > .ib-field-addon-start").TextContent);
    }

    [Fact]
    public void Rejects_non_number_types()
    {
        Assert.ThrowsAny<InvalidOperationException>(() => NumberFor(() => _model.Name));
    }
}
