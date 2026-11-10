using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class SchedulerRenderTests : BunitContext
{
    private sealed class Booking
    {
        public int Id { get; set; }
        public string Room { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    private readonly System.Globalization.CultureInfo _culture = System.Globalization.CultureInfo.CurrentCulture;

    // The names and times read in US English, whatever the machine's culture (CI's is invariant).
    public SchedulerRenderTests()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
    }

    protected override void Dispose(bool disposing)
    {
        System.Globalization.CultureInfo.CurrentCulture = _culture;
        base.Dispose(disposing);
    }

    [Fact]
    public void An_appointment_opens_in_the_form_and_a_save_copies_its_new_times_to_it()
    {
        var booking = new Booking { Id = 7, Room = "Room 2", Start = new DateTime(2026, 11, 9, 9, 0, 0), End = new DateTime(2026, 11, 9, 10, 0, 0) };
        SchedulerSave<Booking>? saved = null;
        var cut = Render<Scheduler<Booking>>(p => p
            .Add(c => c.Items, [booking])
            .Add(c => c.Start, b => b.Start)
            .Add(c => c.End, b => b.End)
            .Add(c => c.Title, b => b.Room)
            .Add(c => c.ItemKey, b => b.Id.ToString())
            .Add(c => c.Date, new DateOnly(2026, 11, 9))
            .Add(c => c.OnSave, (SchedulerSave<Booking> s) => { saved = s; s.CopyTo(s.Item!); }));

        // The appointment is a link to its form, named by its title and whole time.
        var link = cut.Find("a.ib-sched-event");
        Assert.Contains("appointment=7", link.GetAttribute("href"));
        // Linux ICU puts a narrow no-break space before AM.
        Assert.Contains("Room 2, Monday, November 9, 2026, 9:00 AM to 10:00 AM", link.TextContent.Replace(' ', ' '));

        Services.GetRequiredService<NavigationManager>().NavigateTo(link.GetAttribute("href")!);
        cut.Render();
        Assert.Equal("Room 2", cut.Find("input[name='ib-sched.Title']").GetAttribute("value"));

        // An hour later, saved: OnSave has the form's times, and CopyTo sets the booking's.
        cut.Find("input[name='ib-sched.Title']").Change("Room 3");
        cut.FindAll("input[name='ib-sched.StartTime']")[0].Change("10:00");
        cut.FindAll("input[name='ib-sched.EndTime']")[0].Change("11:00");
        cut.Find("form.ib-form").Submit();
        Assert.NotNull(saved);
        Assert.False(saved!.IsNew);
        Assert.Equal(("Room 3", new DateTime(2026, 11, 9, 10, 0, 0), new DateTime(2026, 11, 9, 11, 0, 0)), (booking.Room, booking.Start, booking.End));
        Assert.Empty(cut.FindAll("dialog[open]"));
    }

    [Fact]
    public void A_provider_is_asked_for_the_days_shown_and_again_for_the_next_week()
    {
        var asked = new List<SchedulerRequest>();
        var bookings = new[]
        {
            new Booking { Id = 1, Room = "Room 2", Start = new DateTime(2026, 11, 10, 9, 0, 0), End = new DateTime(2026, 11, 10, 10, 0, 0) },
            new Booking { Id = 2, Room = "Room 5", Start = new DateTime(2026, 11, 17, 9, 0, 0), End = new DateTime(2026, 11, 17, 10, 0, 0) },
        };
        var cut = Render<Scheduler<Booking>>(p => p
            .Add(c => c.ItemsProvider, r =>
            {
                asked.Add(r);
                return ValueTask.FromResult(bookings.Where(b => b.Start < r.End && b.End > r.Start));
            })
            .Add(c => c.Start, b => b.Start)
            .Add(c => c.End, b => b.End)
            .Add(c => c.Title, b => b.Room)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Date, new DateOnly(2026, 11, 9)));

        Assert.Equal((new DateTime(2026, 11, 8), new DateTime(2026, 11, 15), SchedulerView.Week), (asked[^1].Start, asked[^1].End, asked[^1].View));
        Assert.Contains("Room 2", cut.Markup);

        // A parent drawing again asks for nothing new (a save's render mustn't query mid-save).
        var count = asked.Count;
        cut.Render();
        Assert.Equal(count, asked.Count);

        Services.GetRequiredService<NavigationManager>().NavigateTo(cut.Find("a[aria-label='Next week']").GetAttribute("href")!);
        cut.WaitForAssertion(() => Assert.Contains("Room 5", cut.Markup));
        Assert.Equal(new DateTime(2026, 11, 15), asked[^1].Start);
        Assert.DoesNotContain("Room 2", cut.Markup);
    }

    [Fact]
    public void A_provider_that_fails_says_so_and_Retry_loads_again()
    {
        var fail = true;
        var cut = Render<Scheduler<Booking>>(p => p
            .Add(c => c.ItemsProvider, r => fail
                ? throw new InvalidOperationException("down")
                : ValueTask.FromResult<IEnumerable<Booking>>([new Booking { Id = 1, Room = "Room 2", Start = r.Start.AddHours(9), End = r.Start.AddHours(10) }]))
            .Add(c => c.Start, b => b.Start)
            .Add(c => c.End, b => b.End)
            .Add(c => c.Title, b => b.Room)
            .Add(c => c.Date, new DateOnly(2026, 11, 9)));

        Assert.Contains("Couldn't load the appointments.", cut.Find("[role=alert]").TextContent);
        fail = false;
        cut.Find("[role=alert] button").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role=alert]")));
        Assert.Contains("Room 2", cut.Markup);
    }

    [Fact]
    public void An_IQueryable_is_filtered_to_the_days_shown_and_the_categories_not_hidden()
    {
        var all = new[]
        {
            (Id: 1, Start: new DateTime(2026, 11, 9, 9, 0, 0), End: new DateTime(2026, 11, 9, 10, 0, 0), Kind: "work"),
            (Id: 2, Start: new DateTime(2026, 11, 9, 11, 0, 0), End: new DateTime(2026, 11, 9, 12, 0, 0), Kind: "family"),
            (Id: 3, Start: new DateTime(2026, 11, 2, 9, 0, 0), End: new DateTime(2026, 11, 3, 9, 0, 0), Kind: "work"),
            (Id: 4, Start: new DateTime(2026, 11, 7, 0, 0, 0), End: new DateTime(2026, 11, 10, 0, 0, 0), Kind: "work"),
            (Id: 5, Start: new DateTime(2026, 11, 8, 0, 0, 0), End: new DateTime(2026, 11, 8, 0, 0, 0), Kind: "work"),
        }.AsQueryable();

        var shown = SchedulerQuery.InRange(all, a => a.Start, a => a.End, a => a.Kind, new DateTime(2026, 11, 8), new DateTime(2026, 11, 15), ["family"]);

        // In the week: the 9 AM one, one that runs into it, and a moment at its start; not last week's, not a hidden category's.
        Assert.Equal([1, 4, 5], shown.Select(a => a.Id).OrderBy(id => id));
    }

    [Fact]
    public void A_bound_date_and_view_change_in_the_component_not_the_address()
    {
        DateOnly? date = new DateOnly(2026, 11, 9);
        var view = SchedulerView.Week;
        var navigation = Services.GetRequiredService<NavigationManager>();
        var before = navigation.Uri;
        var cut = Render<Scheduler<Booking>>(p => p
            .Add(c => c.Items, [new Booking { Id = 1, Room = "Room 5", Start = new DateTime(2026, 11, 17, 9, 0, 0), End = new DateTime(2026, 11, 17, 10, 0, 0) }])
            .Add(c => c.Start, b => b.Start)
            .Add(c => c.End, b => b.End)
            .Add(c => c.Title, b => b.Room)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Date, date)
            .Add(c => c.DateChanged, d => date = d)
            .Add(c => c.View, view)
            .Add(c => c.ViewChanged, v => view = v));

        cut.Find("a[aria-label='Next week']").Click();
        Assert.Equal(new DateOnly(2026, 11, 16), date);
        cut.Render(p => p.Add(c => c.Date, date));
        Assert.Contains("Room 5", cut.Markup);

        cut.FindAll("[role=menuitemradio]").First(i => i.TextContent.Trim().StartsWith("Month")).Click();
        Assert.Equal(SchedulerView.Month, view);
        Assert.Equal(before, navigation.Uri);
    }

    [Fact]
    public void The_templates_draw_an_appointment_and_add_to_each_day_s_header()
    {
        var cut = Render<Scheduler<Booking>>(p => p
            .Add(c => c.Items, [new Booking { Id = 1, Room = "Room 2", Start = new DateTime(2026, 11, 9, 9, 0, 0), End = new DateTime(2026, 11, 9, 9, 15, 0) }])
            .Add(c => c.Start, b => b.Start)
            .Add(c => c.End, b => b.End)
            .Add(c => c.Title, b => b.Room)
            .Add(c => c.Date, new DateOnly(2026, 11, 9))
            .Add(c => c.AppointmentTemplate, a => $"{a.Item.Room} ({(a.OneLine ? "one line" : "wrapped")})")
            .Add(c => c.DayHeaderTemplate, d => $"Sunny {d.Day}"));

        // A quarter hour's box is one line; screen readers still hear the appointment's own name.
        var box = cut.Find(".ib-sched-col .ib-sched-event");
        Assert.Equal("Room 2 (one line)", box.QuerySelector(".ib-sched-own")!.TextContent);
        Assert.StartsWith("Room 2, Monday, November 9, 2026", box.QuerySelector(".ib-visually-hidden")!.TextContent);
        Assert.Contains(cut.FindAll(".ib-sched-day-head .ib-sched-day-extra"), e => e.TextContent.Trim() == "Sunny 9");
        Assert.Null(cut.Find(".ib-sched-head").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void The_page_s_own_toolbar_items_join_the_toolbar_and_a_placed_part_moves()
    {
        var cut = Render<Scheduler<Booking>>(p => p
            .Add(c => c.Items, [])
            .Add(c => c.Start, b => b.Start)
            .Add(c => c.End, b => b.End)
            .Add(c => c.Title, b => b.Room)
            .Add(c => c.Date, new DateOnly(2026, 11, 9))
            .Add(c => c.ToolbarContent, b =>
            {
                b.OpenComponent<ToolbarButton>(0);
                b.AddComponentParameter(1, nameof(ToolbarButton.ChildContent), (RenderFragment)(t => t.AddContent(0, "Print")));
                b.CloseComponent();
                b.OpenComponent<SchedulerToday>(2);
                b.CloseComponent();
            }));

        // In the order written: the arrows and the period keep their places first, then Print, then Today.
        var start = cut.Find(".ib-toolbar-start").TextContent;
        Assert.True(start.IndexOf("November 2026", StringComparison.Ordinal) < start.IndexOf("Print", StringComparison.Ordinal));
        Assert.True(start.IndexOf("Print", StringComparison.Ordinal) < start.IndexOf("Today", StringComparison.Ordinal));
        Assert.Single(cut.FindAll("a[data-ib-key='t']"));
    }

    [Fact]
    public void A_toolbar_part_turned_on_later_comes_back_in_its_place()
    {
        var cut = Render<Scheduler<Booking>>(p => p
            .Add(c => c.Items, [])
            .Add(c => c.Start, b => b.Start)
            .Add(c => c.End, b => b.End)
            .Add(c => c.Title, b => b.Room)
            .Add(c => c.Date, new DateOnly(2026, 11, 9)));
        Assert.DoesNotContain("Categories", cut.Find(".ib-toolbar-end").TextContent);

        // Categories that arrive after the first render (loaded, say) go before the view menu, as always.
        cut.Render(p => p.Add(c => c.Categories, [new SchedulerCategory("work", "Work")]));
        var end = cut.Find(".ib-toolbar-end").TextContent;
        Assert.True(end.IndexOf("Categories", StringComparison.Ordinal) < end.IndexOf("Week", StringComparison.Ordinal));
    }
}
