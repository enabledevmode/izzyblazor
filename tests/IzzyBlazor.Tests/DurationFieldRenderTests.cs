using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class DurationFieldRenderTests : BunitContext
{
    public class Model
    {
        public TimeSpan? Timeout { get; set; } = new(1, 2, 3, 4);

        public TimeSpan Length { get; set; } = TimeSpan.FromMinutes(90);
    }

    private readonly Model _model = new();
    private readonly EditContext _context;

    public DurationFieldRenderTests()
    {
        _context = new EditContext(_model);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<DurationField<T>> DurationFor<T>(Expression<Func<T>> field, Action<ComponentParameterCollectionBuilder<DurationField<T>>>? more = null) =>
        Render<DurationField<T>>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, field);
            p.Add(f => f.Value, field.Compile()());
            p.Add(f => f.ValueChanged, value => _model.GetType().GetProperty(((MemberExpression)field.Body).Member.Name)!.SetValue(_model, value));
            more?.Invoke(p);
        });

    private static string[] Boxes<T>(IRenderedComponent<DurationField<T>> cut) =>
        cut.FindAll("[data-ib-seconds]").Select(box => box.GetAttribute("value") ?? "").ToArray();

    [Fact]
    public void The_text_input_holds_the_value_in_the_format_that_posts()
    {
        var input = DurationFor(() => _model.Timeout).Find(".ib-duration-text input");

        Assert.Equal("1.02:03:04", input.GetAttribute("value"));
        Assert.EndsWith("Timeout", input.GetAttribute("name"));
    }

    [Fact]
    public void Emptying_a_nullable_field_clears_it_and_a_value_it_cant_read_is_an_error()
    {
        var cut = DurationFor(() => _model.Timeout);
        cut.Find(".ib-duration-text input").Change("");
        Assert.Null(_model.Timeout);

        cut.Find(".ib-duration-text input").Change("soon");
        Assert.Contains("The Timeout field must be a length of time.", _context.GetValidationMessages());
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(10, false)]
    public void Min_and_max_hold_when_the_form_validates(int minutes, bool valid)
    {
        _model.Length = TimeSpan.FromMinutes(minutes);
        DurationFor(() => _model.Length, p => p.Add(f => f.Min, TimeSpan.FromMinutes(15)).Add(f => f.Max, TimeSpan.FromHours(1)));

        Assert.Equal(valid, _context.Validate());
    }
}
