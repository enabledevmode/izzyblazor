using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class CalendarRenderTests : BunitContext
{
    public class Model
    {
        [Range(typeof(DateOnly), "2027-03-05", "2027-03-20")]
        public DateOnly? Annotated { get; set; } = new DateOnly(2027, 3, 10);

        public DateOnly Day { get; set; } = new(2027, 2, 15);

        public DateOnly? Empty { get; set; }

        public DateTime When { get; set; }
    }

    private readonly Model _model = new();
    private readonly EditContext _context;

    public CalendarRenderTests()
    {
        _context = new EditContext(_model);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<Calendar<T>> CalendarFor<T>(Expression<Func<T>> field, Action<ComponentParameterCollectionBuilder<Calendar<T>>>? more = null, string culture = "en-US") =>
        Render<Calendar<T>>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, field);
            p.Add(f => f.Value, field.Compile()());
            p.Add(f => f.ValueChanged, value => _model.GetType().GetProperty(((MemberExpression)field.Body).Member.Name)!.SetValue(_model, value));
            p.Add(f => f.Culture, new CultureInfo(culture));
            more?.Invoke(p);
        });

    [Fact]
    public void The_month_of_the_value_shows_six_weeks_named_for_screen_readers_with_the_day_chosen()
    {
        var cut = CalendarFor(() => _model.Day);
        var grid = cut.Find("[role=grid]");

        var names = grid.GetAttribute("aria-labelledby")!.Split(' ').Select(id => cut.Find("#" + id).TextContent).ToList();
        Assert.Equal(["Day", "February 2027"], names);
        Assert.Equal(42, cut.FindAll("tbody td").Count);
        Assert.Equal("Sunday", cut.Find("thead th").GetAttribute("abbr"));
        Assert.Equal("Monday, February 15, 2027", cut.Find("td[aria-selected=true] button").GetAttribute("aria-label"));
        Assert.Single(cut.FindAll("td[aria-selected]"));
        Assert.Equal("2027-02-15", cut.Find("input").GetAttribute("value"));
    }

    [Theory]
    [InlineData("en-US", null, "Sunday", "February 2027")]
    [InlineData("de-DE", null, "Montag", "Februar 2027")]
    [InlineData("en-US", DayOfWeek.Wednesday, "Wednesday", "February 2027")]
    [InlineData("th-TH", null, null, "2027")]
    public void Names_and_the_first_day_follow_the_culture_on_the_gregorian_calendar(string culture, DayOfWeek? first, string? firstColumn, string month)
    {
        var cut = CalendarFor(() => _model.Day, p => p.Add(f => f.FirstDayOfWeek, first), culture);

        Assert.Contains(month, cut.Find(".ib-calendar-month").TextContent);
        if (firstColumn is not null)
        {
            Assert.Equal(firstColumn, cut.Find("thead th").GetAttribute("abbr"));
        }
    }

    [Fact]
    public void Limits_come_from_the_model_and_days_outside_them_are_marked()
    {
        var cut = CalendarFor(() => _model.Annotated);

        Assert.Equal("2027-03-05", cut.Find("input").GetAttribute("min"));
        Assert.Equal("2027-03-20", cut.Find("input").GetAttribute("max"));
        Assert.Equal("true", cut.Find("button[aria-label='Thursday, March 4, 2027']").GetAttribute("aria-disabled"));
        Assert.Null(cut.Find("button[aria-label='Friday, March 5, 2027']").GetAttribute("aria-disabled"));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(1, false)]
    [InlineData(4, false)]
    public void Min_and_max_hold_when_the_form_validates(int month, bool valid)
    {
        _model.Day = new DateOnly(2027, month, 15);
        CalendarFor(() => _model.Day, p => p.Add(f => f.Min, new DateOnly(2027, 2, 1)).Add(f => f.Max, new DateOnly(2027, 3, 31)));

        Assert.Equal(valid, _context.Validate());
    }

    [Fact]
    public void With_no_value_it_shows_the_nearest_month_that_can_be_chosen()
    {
        var cut = CalendarFor(() => _model.Empty, p => p.Add(f => f.Max, new DateOnly(2001, 5, 20)));

        Assert.Equal("May 2001", cut.Find(".ib-calendar-month").TextContent);
        Assert.Empty(cut.FindAll("td[aria-selected]"));
    }

    [Fact]
    public void Week_numbers_count_as_the_culture_does_and_adjacent_days_can_be_left_out()
    {
        _model.Day = new DateOnly(2024, 12, 15);
        var cut = CalendarFor(() => _model.Day, p => p.Add(f => f.ShowWeekNumbers, true).Add(f => f.ShowAdjacentDays, false), "de-DE");

        // December 30, 2024 is ISO week 1 of 2025.
        var weeks = cut.FindAll("tbody th").Select(th => th.TextContent).ToList();
        Assert.Equal(["48", "49", "50", "51", "52", "1"], weeks);
        Assert.Equal(31, cut.FindAll("tbody button:not([hidden])").Count);
    }

    [Fact]
    public void On_an_interactive_page_a_new_value_reaches_the_script_and_the_cells_stay_as_first_drawn()
    {
        var cut = CalendarFor(() => _model.Day);

        cut.Render(p => p.Add(f => f.Value, new DateOnly(2027, 8, 20)));

        Assert.Equal("2027-08-20", cut.Find("ib-calendar").GetAttribute("data-ib-value"));
        Assert.Equal("February 2027", cut.Find(".ib-calendar-month").TextContent);
        Assert.Equal("Monday, February 15, 2027", cut.Find("td[aria-selected=true] button").GetAttribute("aria-label"));
    }

    [Fact]
    public void The_ends_of_the_calendar_draw_without_throwing()
    {
        _model.Day = DateOnly.MaxValue;
        Assert.Contains("9999", CalendarFor(() => _model.Day).Find(".ib-calendar-month").TextContent);

        _model.Empty = new DateOnly(1, 1, 2);
        Assert.Contains("0001", CalendarFor(() => _model.Empty).Find(".ib-calendar-month").TextContent);
    }

    [Fact]
    public void A_type_that_isnt_a_date_says_so()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render<Calendar<DateTime>>(p =>
        {
            p.AddCascadingValue(_context);
            p.Add(f => f.ValueExpression, () => _model.When);
        }));
        Assert.Contains("DateOnly", error.Message);
    }
}
