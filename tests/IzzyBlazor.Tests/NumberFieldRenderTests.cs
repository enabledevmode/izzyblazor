using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class NumberFieldRenderTests : BunitContext
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

    public NumberFieldRenderTests()
    {
        _context = new EditContext(_model);
    }

    private IRenderedComponent<NumberField<T>> NumberFor<T>(Expression<Func<T>> field, Action<ComponentParameterCollectionBuilder<NumberField<T>>>? more = null) =>
        Render<NumberField<T>>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, field);
            p.Add(f => f.Value, field.Compile()());
            more?.Invoke(p);
        });

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

    [Theory]
    [InlineData("1E-05", 0.00001)]
    [InlineData("-2.5", -2.5)]
    public void Parses_invariant_numbers_with_exponents(string text, double expected)
    {
        Assert.True(NumberTypes.TryParse<double>(text, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Parsing_rejects_junk_and_allows_empty_only_for_nullable()
    {
        Assert.False(NumberTypes.TryParse<int>("1.5", out _));
        Assert.False(NumberTypes.TryParse<decimal>("abc", out _));
        Assert.False(NumberTypes.TryParse<int>("", out _));
        Assert.True(NumberTypes.TryParse<int?>("", out var empty));
        Assert.Null(empty);
        Assert.True(NumberTypes.TryParse<decimal?>("12.50", out var price));
        Assert.Equal(12.50m, price);
    }
}
