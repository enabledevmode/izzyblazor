using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class DateRangeFieldTests : BunitContext
{
    public class Model
    {
        public DateRange? Stay { get; set; }
    }

    private readonly Model _model = new();
    private readonly EditContext _context;

    public DateRangeFieldTests()
    {
        _context = new EditContext(_model);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private static DateOnly Day(int month, int day) => new(2027, month, day);

    private IRenderedComponent<DateRangeField> Field(DateRange? value, Action<ComponentParameterCollectionBuilder<DateRangeField>>? more = null)
    {
        _model.Stay = value;
        return Render<DateRangeField>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, () => _model.Stay);
            p.Add(f => f.Value, _model.Stay);
            p.Add(f => f.ValueChanged, v => _model.Stay = v);
            more?.Invoke(p);
        });
    }

    private string[] Errors()
    {
        _context.Validate();
        return _context.GetValidationMessages().ToArray();
    }

    [Fact]
    public void It_is_a_group_with_a_labelled_input_for_each_end()
    {
        var cut = Field(new(Day(3, 8), Day(3, 12)), p => p.Add(f => f.Min, Day(3, 1)).Add(f => f.Label, "Stay"));

        Assert.Equal("Stay", cut.Find("fieldset legend").TextContent.Trim());
        var inputs = cut.FindAll("input[type=date]");
        Assert.Equal(new[] { "2027-03-08", "2027-03-12" }, inputs.Select(i => i.GetAttribute("value")));
        Assert.Equal(new[] { "Start", "End" }, cut.FindAll("label").Select(l => l.TextContent.Trim()));
        Assert.Equal(inputs[1].Id, cut.FindAll("label")[1].GetAttribute("for"));
        Assert.Equal("2027-03-01", inputs[0].GetAttribute("min"));
    }

    [Fact]
    public void The_end_cant_be_chosen_before_the_start()
    {
        var cut = Field(new(Day(3, 8), null), p => p.Add(f => f.Min, Day(3, 1)));

        Assert.Equal("2027-03-08", cut.FindAll("input")[1].GetAttribute("min"));
    }

    [Fact]
    public void Each_input_changes_its_own_end_and_clearing_both_is_no_value()
    {
        var cut = Field(new(Day(3, 8), Day(3, 12)));

        cut.FindAll("input")[1].Change("2027-03-20");
        Assert.Equal(new DateRange(Day(3, 8), Day(3, 20)), _model.Stay);

        cut.FindAll("input")[1].Change("");
        Assert.Equal(new DateRange(Day(3, 8), null), _model.Stay);

        cut.FindAll("input")[0].Change("");
        Assert.Null(_model.Stay);
    }

    [Fact]
    public void A_good_range_passes_every_rule()
    {
        Field(new(Day(3, 8), Day(3, 12)), p => p.Add(f => f.Min, Day(3, 1)).Add(f => f.Max, Day(3, 31)).Add(f => f.MinDays, 5).Add(f => f.MaxDays, 5).Add(f => f.DateDisabled, d => d == Day(3, 20)));

        Assert.Empty(Errors());
    }

    [Fact]
    public void A_range_ending_before_it_starts_is_turned_away()
    {
        Field(new(Day(3, 12), Day(3, 8)), p => p.Add(f => f.Label, "Stay"));

        Assert.Equal(new[] { "Stay can't end before it starts." }, Errors());
    }

    [Fact]
    public void Each_end_is_held_to_the_limits()
    {
        Field(new(Day(2, 20), Day(3, 8)), p => p.Add(f => f.Label, "Stay").Add(f => f.Min, Day(3, 1)));
        Assert.Contains("or later", Assert.Single(Errors()));
    }

    [Theory]
    [InlineData(2, null, "at least 2")]
    [InlineData(null, 2, "at most 2")]
    public void Its_length_is_counted_with_both_ends(int? min, int? max, string said)
    {
        var (start, end) = min is null ? (8, 10) : (8, 8);
        Field(new(Day(3, start), Day(3, end)), p => p.Add(f => f.Label, "Stay").Add(f => f.MinDays, min).Add(f => f.MaxDays, max));

        Assert.Contains(said, Assert.Single(Errors()));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void A_disabled_day_inside_is_turned_away_unless_allowed(bool allowed, int errors)
    {
        Field(new(Day(3, 8), Day(3, 12)), p => p.Add(f => f.DateDisabled, d => d == Day(3, 10)).Add(f => f.AllowDisabledInRange, allowed));

        Assert.Equal(errors, Errors().Length);
    }

    [Fact]
    public void A_disabled_day_is_never_an_end()
    {
        Field(new(Day(3, 8), Day(3, 12)), p => p.Add(f => f.DateDisabled, d => d == Day(3, 12)).Add(f => f.AllowDisabledInRange, true));

        Assert.Single(Errors());
    }

    [Fact]
    public void Required_needs_both_ends_and_open_ends_are_fine_otherwise()
    {
        var open = Field(new(Day(3, 8), null));
        Assert.Empty(Errors());
        open.Dispose();

        Field(new(Day(3, 8), null), p => p.Add(f => f.Required, true).Add(f => f.Label, "Stay"));
        Assert.Equal(new[] { "Stay needs a start and an end." }, Errors());
    }

    [Fact]
    public void Required_turns_away_a_range_with_no_ends_as_a_post_can_bind()
    {
        Field(new DateRange(), p => p.Add(f => f.Required, true).Add(f => f.Label, "Stay"));

        Assert.Single(Errors());
    }

    [Fact]
    public void A_range_contains_its_ends_and_all_of_the_last_day()
    {
        var range = new DateRange(Day(3, 8), Day(3, 12));

        Assert.True(range.Contains(Day(3, 8)));
        Assert.True(range.Contains(new DateTime(2027, 3, 12, 23, 59, 59)));
        Assert.False(range.Contains(new DateTime(2027, 3, 13, 0, 0, 0)));
        Assert.True(new DateRange(null, Day(3, 12)).Contains(Day(1, 1)));
        Assert.Equal(5, range.Days);
        Assert.Null(new DateRange(Day(3, 8), null).Days);
    }

    [Fact]
    public void To_utc_ends_at_the_first_instant_of_the_next_day_across_a_change_of_the_clocks()
    {
        var york = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        // The clocks go forward on March 14, 2027, so that day is 23 hours long.
        var (from, until) = new DateRange(Day(3, 14), Day(3, 14)).ToUtc(york);

        Assert.Equal(new DateTimeOffset(2027, 3, 14, 5, 0, 0, TimeSpan.Zero), from);
        Assert.Equal(new DateTimeOffset(2027, 3, 15, 4, 0, 0, TimeSpan.Zero), until);
        Assert.Equal((null, null), new DateRange().ToUtc(york));
    }

    [Fact]
    public void To_utc_takes_the_first_midnight_where_the_clocks_repeat_it()
    {
        // Havana's clocks go back at 01:00 on Nov 7, 2027, so midnight comes twice.
        var havana = TimeZoneInfo.FindSystemTimeZoneById("America/Havana");

        var (from, _) = new DateRange(new DateOnly(2027, 11, 7), null).ToUtc(havana);

        Assert.Equal(new DateTimeOffset(2027, 11, 7, 4, 0, 0, TimeSpan.Zero), from);
    }
}
