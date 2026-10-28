using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class DateFieldRenderTests : BunitContext
{
    public class Model
    {
        [Range(typeof(DateOnly), "2026-01-01", "2026-12-31")]
        public DateOnly? Due { get; set; } = new DateOnly(2026, 3, 4);

        public DateTime StartsAt { get; set; } = new(2026, 10, 27, 14, 30, 0, DateTimeKind.Utc);

        public DateTimeOffset? EndsAt { get; set; }

        [DataType(DataType.Date)]
        public DateTimeOffset? Day { get; set; }

        public string? Name { get; set; }
    }

    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly Model _model = new();
    private readonly EditContext _context;

    public DateFieldRenderTests()
    {
        _context = new EditContext(_model);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<DateField<T>> DateFor<T>(Expression<Func<T>> field, Action<ComponentParameterCollectionBuilder<DateField<T>>>? more = null) =>
        Render<DateField<T>>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, field);
            p.Add(f => f.Value, field.Compile()());
            p.Add(f => f.ValueChanged, value => _model.GetType().GetProperty(((MemberExpression)field.Body).Member.Name)!.SetValue(_model, value));
            more?.Invoke(p);
        });

    [Fact]
    public void A_date_renders_a_date_input_with_the_range_from_annotations()
    {
        var input = DateFor(() => _model.Due).Find("input");

        Assert.Equal("date", input.GetAttribute("type"));
        Assert.Equal("2026-03-04", input.GetAttribute("value"));
        Assert.Equal("2026-01-01", input.GetAttribute("min"));
        Assert.Equal("2026-12-31", input.GetAttribute("max"));
        Assert.EndsWith("Due", input.GetAttribute("name"));
    }

    [Fact]
    public void Times_take_a_date_and_time_unless_the_property_or_field_says_date()
    {
        Assert.Equal("datetime-local", DateFor(() => _model.StartsAt).Find("input").GetAttribute("type"));
        Assert.Equal("date", DateFor(() => _model.Day).Find("input").GetAttribute("type"));
        Assert.Equal("date", DateFor(() => _model.StartsAt, p => p.Add(f => f.Type, DateFieldType.Date)).Find("input").GetAttribute("type"));
    }

    [Fact]
    public void Times_show_and_parse_in_the_time_zone()
    {
        // 14:30 UTC is 15:30 in Berlin in late October (after the clocks go back on the 25th).
        Assert.Equal("2026-10-27T14:30", DateFor(() => _model.StartsAt).Find("input").GetAttribute("value"));
        var berlin = DateFor(() => _model.StartsAt, p => p.Add(f => f.TimeZone, Berlin));
        Assert.Equal("2026-10-27T15:30", berlin.Find("input").GetAttribute("value"));

        // Typed in summer, in Berlin: two hours ahead. A DateTime comes back in UTC.
        berlin.Find("input").Change("2026-07-01T09:00");
        Assert.Equal(new DateTime(2026, 7, 1, 7, 0, 0, DateTimeKind.Utc), _model.StartsAt);
        Assert.Equal(DateTimeKind.Utc, _model.StartsAt.Kind);

        // A DateTimeOffset keeps the zone's offset.
        DateFor(() => _model.EndsAt, p => p.Add(f => f.TimeZone, Berlin)).Find("input").Change("2026-12-24T18:00");
        Assert.Equal(new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.FromHours(1)), _model.EndsAt);
        Assert.Equal(TimeSpan.FromHours(1), _model.EndsAt!.Value.Offset);

        // With no zone set, UTC.
        DateFor(() => _model.EndsAt).Find("input").Change("2026-12-24T18:00");
        Assert.Equal(TimeSpan.Zero, _model.EndsAt!.Value.Offset);
    }

    [Fact]
    public void A_date_for_a_time_is_midnight_in_the_zone()
    {
        DateFor(() => _model.Day, p => p.Add(f => f.TimeZone, Berlin)).Find("input").Change("2026-07-01");
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(2)), _model.Day);
    }

    [Theory]
    [InlineData(null, null, "2026-10-27T14:30")]
    [InlineData(1, "1", "2026-10-27T14:30:00")]
    [InlineData(900, "900", "2026-10-27T14:30")]
    public void A_step_under_a_minute_shows_seconds(int? seconds, string? step, string value)
    {
        var input = DateFor(() => _model.StartsAt, p => p.Add(f => f.Step, seconds is { } s ? TimeSpan.FromSeconds(s) : null)).Find("input");

        Assert.Equal(step, input.GetAttribute("step"));
        Assert.Equal(value, input.GetAttribute("value"));
    }

    [Fact]
    public void Min_and_max_parameters_replace_the_annotations()
    {
        var input = DateFor(() => _model.Due, p => p.Add(f => f.Min, new DateOnly(2026, 6, 1)).Add(f => f.Max, (DateOnly?)null)).Find("input");

        Assert.Equal("2026-06-01", input.GetAttribute("min"));
        Assert.Equal("2026-12-31", input.GetAttribute("max"));
    }

    [Fact]
    public void Emptying_a_nullable_field_clears_it_and_a_value_it_cant_read_is_an_error()
    {
        var cut = DateFor(() => _model.Due);
        cut.Find("input").Change("");
        Assert.Null(_model.Due);

        cut.Find("input").Change("27/10/2026");
        Assert.Contains("The Due field must be a date.", _context.GetValidationMessages());
    }

    [Fact]
    public void A_time_with_no_utc_equivalent_is_an_error_not_a_crash()
    {
        // Midnight on January 1 of year 1 in Tokyo is nine hours before the calendar starts in UTC.
        var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        DateFor(() => _model.StartsAt, p => p.Add(f => f.TimeZone, tokyo)).Find("input").Change("0001-01-01T00:00");
        Assert.Contains("The Starts at field must be a date.", _context.GetValidationMessages());
    }

    [Theory]
    [InlineData(false, "+00:00", "offset")]
    [InlineData(true, null, null)]
    public void A_static_page_tells_the_script_which_zone_a_post_is_in(bool interactive, string? zone, string? post)
    {
        SetRendererInfo(new RendererInfo(interactive ? "Server" : "Static", interactive));
        var input = DateFor(() => _model.EndsAt).Find("input");

        Assert.Equal(zone, input.GetAttribute("data-ib-zone"));
        Assert.Equal(post, input.GetAttribute("data-ib-post"));
    }

    [Fact]
    public void A_static_datetime_needs_converting_only_from_another_zone()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));

        Assert.False(DateFor(() => _model.StartsAt).Find("input").HasAttribute("data-ib-zone"));
        Assert.False(DateFor(() => _model.Due).Find("input").HasAttribute("data-ib-zone"));
        var berlin = DateFor(() => _model.StartsAt, p => p.Add(f => f.TimeZone, Berlin)).Find("input");
        Assert.Equal("Europe/Berlin", berlin.GetAttribute("data-ib-zone"));
        Assert.Equal("utc", berlin.GetAttribute("data-ib-post"));
    }

    [Fact]
    public void It_refuses_types_it_cant_show_and_a_time_on_a_date()
    {
        Assert.Throws<InvalidOperationException>(() => DateFor(() => _model.Name));
        Assert.Throws<InvalidOperationException>(() => DateFor(() => _model.Due, p => p.Add(f => f.Type, DateFieldType.DateTime)));
    }
}
