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

    public SchedulerRenderTests() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));

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
}
