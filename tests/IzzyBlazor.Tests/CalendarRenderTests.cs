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
    public void Min_and_max_hold_when_the_form_validates(int month, bool valid)
    {
        _model.Day = new DateOnly(2027, month, 15);
        CalendarFor(() => _model.Day, p => p.Add(f => f.Min, new DateOnly(2027, 2, 1)).Add(f => f.Max, new DateOnly(2027, 3, 31)));

        Assert.Equal(valid, _context.Validate());
    }

    [Theory]
    [InlineData(13, false)]
    [InlineData(15, true)]
    public void A_disabled_day_is_refused_when_the_form_validates(int day, bool valid)
    {
        _model.Day = new DateOnly(2027, 3, day);
        CalendarFor(() => _model.Day, p => p.Add(f => f.DateDisabled, date => date.DayOfWeek == DayOfWeek.Saturday));

        Assert.Equal(valid, _context.Validate());
    }
}
