namespace IzzyBlazor.Tests;

// What a timeline's look doesn't say on its own: a done step is announced, an avatar marker stays out of
// the reading, and the time is read before the title wherever it's placed.
public class TimelineTests : BunitContext
{
    [Fact]
    public void Done_steps_avatars_and_the_time_read_in_order()
    {
        var timeline = Render<Timeline>(p => p
            .Add(t => t.TimePlacement, TimelinePlacement.Start)
            .Add(t => t.Marker, TimelineMarker.Ring)
            .AddChildContent<TimelineItem>(i => i.Add(x => x.Title, "Shipped").Add(x => x.Time, "Mar 18").Add(x => x.Done, true).Add(x => x.Badge, "2d")));
        var list = timeline.Find("ol");
        Assert.Contains("ib-timeline-time-start", list.ClassName);
        Assert.Contains("ib-timeline-ring", list.ClassName);

        var item = timeline.Find("li");
        Assert.Contains("ib-timeline-done", item.ClassName);
        Assert.Equal("Shipped (completed)", item.QuerySelector(".ib-timeline-title")!.TextContent);
        Assert.NotNull(item.QuerySelector(".ib-timeline-marker[aria-hidden='true'] .ib-icon"));
        Assert.Equal("2d", item.QuerySelector(".ib-timeline-head .ib-badge")!.TextContent.Trim());
        // The time comes before the title in the body, wherever the CSS puts it.
        var body = item.QuerySelector(".ib-timeline-body")!;
        Assert.Equal("ib-timeline-time", body.FirstElementChild!.ClassName);

        var feed = Render<TimelineItem>(p => p.Add(x => x.Avatar, "Sara Costa")
            .Add(x => x.TitleContent, b => { b.OpenElement(0, "strong"); b.AddContent(1, "Sara Costa"); b.CloseElement(); b.AddContent(2, " sent it"); }));
        Assert.Contains("ib-timeline-has-avatar", feed.Find("li").ClassName);
        Assert.NotNull(feed.Find(".ib-timeline-marker[aria-hidden='true'] .ib-avatar"));
        Assert.Equal("Sara Costa sent it", feed.Find(".ib-timeline-title-rich").TextContent);
    }
}
