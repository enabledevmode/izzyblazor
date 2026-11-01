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
    public void A_value_past_the_largest_unit_shows_in_it()
    {
        Assert.Equal(["26", "3", "4"], Boxes(DurationFor(() => _model.Timeout)));
        Assert.Equal(["1", "2", "3", "4"], Boxes(DurationFor(() => _model.Timeout, p => p.Add(f => f.LargestUnit, DurationUnit.Days))));
    }

    [Fact]
    public void Each_box_stops_where_the_next_unit_starts_except_the_largest()
    {
        var max = DurationFor(() => _model.Timeout, p => p.Add(f => f.LargestUnit, DurationUnit.Days))
            .FindAll("[data-ib-seconds]").Select(box => box.GetAttribute("max") ?? "none").ToArray();

        Assert.Equal(["none", "23", "59", "59"], max);
    }

    [Fact]
    public void A_sign_shows_only_when_min_or_the_value_is_negative()
    {
        Assert.Empty(DurationFor(() => _model.Length).FindAll("select"));
        Assert.NotEmpty(DurationFor(() => _model.Length, p => p.Add(f => f.Min, TimeSpan.FromHours(-1))).FindAll("select"));

        _model.Length = TimeSpan.FromMinutes(-5);
        Assert.Equal(["0", "5", "0"], Boxes(DurationFor(() => _model.Length)));
    }

    [Fact]
    public void A_step_goes_on_the_smallest_box_and_must_fit_it()
    {
        var minutes = DurationFor(() => _model.Length, p => p.Add(f => f.SmallestUnit, DurationUnit.Minutes).Add(f => f.Step, TimeSpan.FromMinutes(15)));
        Assert.Equal("15", minutes.FindAll("[data-ib-seconds]").Last().GetAttribute("step"));

        Assert.Throws<InvalidOperationException>(() => DurationFor(() => _model.Length, p => p.Add(f => f.Step, TimeSpan.FromMinutes(15))));
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
    [InlineData(70, false)]
    public void Min_and_max_hold_when_the_form_validates(int minutes, bool valid)
    {
        _model.Length = TimeSpan.FromMinutes(minutes);
        DurationFor(() => _model.Length, p => p.Add(f => f.Min, TimeSpan.FromMinutes(15)).Add(f => f.Max, TimeSpan.FromHours(1)));

        Assert.Equal(valid, _context.Validate());
    }
}
