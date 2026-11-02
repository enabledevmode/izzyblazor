using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class TimeFieldRenderTests : BunitContext
{
    public class Model
    {
        [Range(typeof(TimeOnly), "06:00", "12:00")]
        public TimeOnly? Opens { get; set; } = new TimeOnly(9, 30, 20);

        public TimeOnly Night { get; set; } = new(23, 0);
    }

    private readonly Model _model = new();
    private readonly EditContext _context;

    public TimeFieldRenderTests()
    {
        _context = new EditContext(_model);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<TimeField<T>> TimeFor<T>(Expression<Func<T>> field, Action<ComponentParameterCollectionBuilder<TimeField<T>>>? more = null) =>
        Render<TimeField<T>>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, field);
            p.Add(f => f.Value, field.Compile()());
            p.Add(f => f.ValueChanged, value => _model.GetType().GetProperty(((MemberExpression)field.Body).Member.Name)!.SetValue(_model, value));
            more?.Invoke(p);
        });

    private static string[] Slots(IRenderedComponent<TimeField<TimeOnly?>> cut) =>
        cut.FindAll("[role=option]").Select(o => o.GetAttribute("data-value")!).ToArray();

    [Fact]
    public void The_list_runs_every_step_within_the_limits()
    {
        var cut = TimeFor(() => _model.Opens, p => p.Add(f => f.Min, new TimeOnly(9, 0)).Add(f => f.Max, new TimeOnly(10, 0)).Add(f => f.Step, TimeSpan.FromMinutes(20)));

        Assert.Equal(new[] { "09:00", "09:20", "09:40", "10:00" }, Slots(cut));
    }

    [Fact]
    public void With_no_step_the_list_is_half_hours_and_the_input_takes_any_minute()
    {
        var cut = TimeFor(() => _model.Night);

        Assert.Equal(48, cut.FindAll("[role=option]").Count);
        Assert.False(cut.Find("input").HasAttribute("step"));
    }

    [Fact]
    public void A_min_with_seconds_starts_the_list_at_the_next_minute()
    {
        var cut = TimeFor(() => _model.Opens, p => p.Add(f => f.Min, new TimeOnly(9, 0, 30)).Add(f => f.Max, new TimeOnly(9, 3)).Add(f => f.Step, TimeSpan.FromMinutes(1)));

        Assert.Equal(new[] { "09:01", "09:02", "09:03" }, Slots(cut));
    }

    [Fact]
    public void A_window_across_midnight_runs_through_it()
    {
        var cut = TimeFor(() => _model.Opens, p => p.Add(f => f.Min, new TimeOnly(23, 0)).Add(f => f.Max, new TimeOnly(1, 0)).Add(f => f.Step, TimeSpan.FromHours(1)));

        Assert.Equal(new[] { "23:00", "00:00", "01:00" }, Slots(cut));
    }

    [Fact]
    public void A_disabled_time_is_marked_and_turned_away()
    {
        var cut = TimeFor(() => _model.Opens, p => p.Add(f => f.Min, new TimeOnly(9, 0)).Add(f => f.Max, new TimeOnly(10, 0)).Add(f => f.Step, TimeSpan.FromHours(1))
            .Add(f => f.TimeDisabled, t => t.Hour == 9).Add(f => f.Label, "Opens"));

        Assert.Equal("true", cut.Find("[data-value='09:00']").GetAttribute("aria-disabled"));
        Assert.Null(cut.Find("[data-value='10:00']").GetAttribute("aria-disabled"));
        Assert.NotEmpty(cut.FindAll(".ib-date-own"));
        _context.Validate();
        Assert.Contains("can't be", Assert.Single(_context.GetValidationMessages()));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void There_is_no_panel_with_the_browsers_picker_or_nothing_to_change(bool native, bool readOnly, bool disabled)
    {
        var cut = TimeFor(() => _model.Opens, p => p.Add(f => f.UseNative, native).Add(f => f.ReadOnly, readOnly).Add(f => f.Disabled, disabled));

        Assert.Empty(cut.FindAll("ib-time-slots"));
    }

    [Theory]
    [InlineData(30, 3)]
    [InlineData(60, 2)]
    public void Where_a_list_would_be_too_long_it_has_boxes_and_ok(int stepSeconds, int boxes)
    {
        var cut = TimeFor(() => _model.Opens, p => p.Add(f => f.Step, TimeSpan.FromSeconds(stepSeconds)));

        Assert.Empty(cut.FindAll("[role=option]"));
        Assert.Equal(boxes, cut.FindAll(".ib-calendar-time input").Count);
        Assert.All(cut.FindAll(".ib-calendar-time input"), box => Assert.False(box.HasAttribute("name")));
        Assert.NotEmpty(cut.FindAll("[data-ib-ok]"));
    }

    [Fact]
    public void A_time_renders_a_time_input_with_the_range_from_annotations()
    {
        var input = TimeFor(() => _model.Opens).Find("input");

        Assert.Equal("time", input.GetAttribute("type"));
        Assert.Equal("09:30", input.GetAttribute("value"));
        Assert.Equal("06:00", input.GetAttribute("min"));
        Assert.Equal("12:00", input.GetAttribute("max"));
        Assert.Null(input.GetAttribute("step"));
    }

    [Fact]
    public void A_step_is_counted_from_midnight_and_under_a_minute_shows_seconds()
    {
        var quarters = TimeFor(() => _model.Night, p => p.Add(f => f.Step, TimeSpan.FromMinutes(15))).Find("input");
        Assert.Equal("900", quarters.GetAttribute("step"));
        Assert.Equal("00:00", quarters.GetAttribute("min"));

        var seconds = TimeFor(() => _model.Opens, p => p.Add(f => f.Step, TimeSpan.FromSeconds(1))).Find("input");
        Assert.Equal("09:30:20", seconds.GetAttribute("value"));
        Assert.Equal("06:00", seconds.GetAttribute("min"));
    }

    [Fact]
    public void Emptying_a_nullable_field_clears_it_and_a_value_it_cant_read_is_an_error()
    {
        var cut = TimeFor(() => _model.Opens);
        cut.Find("input").Change("07:45");
        Assert.Equal(new TimeOnly(7, 45), _model.Opens);

        cut.Find("input").Change("");
        Assert.Null(_model.Opens);

        cut.Find("input").Change("7pm");
        Assert.Contains("The Opens field must be a time.", _context.GetValidationMessages());
    }

    [Theory]
    [InlineData(22, 6, 23, true)]
    [InlineData(22, 6, 3, true)]
    [InlineData(22, 6, 12, false)]
    [InlineData(9, 17, 12, true)]
    [InlineData(9, 17, 18, false)]
    [InlineData(9, 17, 8, false)]
    public void Min_and_max_hold_when_the_form_validates_and_min_after_max_crosses_midnight(int min, int max, int hour, bool valid)
    {
        _model.Night = new TimeOnly(hour, 0);
        TimeFor(() => _model.Night, p => p.Add(f => f.Min, new TimeOnly(min, 0)).Add(f => f.Max, new TimeOnly(max, 0)));

        Assert.Equal(valid, _context.Validate());
    }
}
