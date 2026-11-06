using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class FeedbackRenderTests : BunitContext
{
    // Services must be registered before anything starts the renderer, SetRendererInfo included.
    private readonly ToastService _toasts = new(new ServiceCollection().BuildServiceProvider());
    private readonly Announcer _announcer = new(new ServiceCollection().BuildServiceProvider());

    public FeedbackRenderTests()
    {
        Services.AddSingleton(_toasts);
        Services.AddSingleton(_announcer);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    [Theory]
    [InlineData(Status.Info, "status")]
    [InlineData(Status.Danger, "alert")]
    public void Alert_role_follows_urgency(Status status, string role)
    {
        var alert = Render<Alert>(p => p.Add(a => a.Status, status).AddChildContent("Message")).Find(".ib-alert");

        Assert.Equal(role, alert.GetAttribute("role"));
        Assert.Contains($"ib-alert-{status.ToString().ToLowerInvariant()}", alert.ClassName);
        Assert.NotNull(alert.QuerySelector(".ib-alert-icon"));
    }

    [Fact]
    public void Dismissible_alert_works_without_script_and_reports_to_csharp()
    {
        bool? reported = null;
        var cut = Render<Alert>(p => p
            .Add(a => a.Id, "a1")
            .Add(a => a.Dismissible, true)
            .Add(a => a.DismissedChanged, (bool d) => reported = d)
            .AddChildContent("Hi"));

        var input = cut.Find("input.ib-alert-dismiss");
        Assert.Equal("a1-dismiss", input.Id);
        Assert.Equal("Dismiss", input.GetAttribute("aria-label"));
        Assert.Equal("a1-dismiss", cut.Find("label.ib-alert-close").GetAttribute("for"));

        // With the script, a real button takes the checkbox's place (CSS swaps them on :defined).
        var button = cut.Find("ib-dismiss > button.ib-alert-close");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal("Dismiss", button.GetAttribute("aria-label"));

        input.Change(true);
        Assert.True(reported);
        Assert.Empty(cut.FindAll(".ib-alert"));

        cut.Render(p => p.Add(a => a.Dismissed, false));
        Assert.Empty(cut.FindAll(".ib-alert"));
        cut.Render(p => p.Add(a => a.Dismissed, true));
        cut.Render(p => p.Add(a => a.Dismissed, false));
        Assert.Single(cut.FindAll(".ib-alert"));
    }

    [Fact]
    public void Toast_region_renders_toasts_from_a_static_response()
    {
        _toasts.Success("Saved it.", "Saved");

        var cut = Render<ToastRegion>(p => p.Add(r => r.Duration, TimeSpan.FromSeconds(3)));

        var region = cut.Find("ib-toasts");
        Assert.Equal("region", region.GetAttribute("role"));
        Assert.Equal("Notifications", region.GetAttribute("aria-label"));
        Assert.Equal("3000", region.GetAttribute("data-duration"));
        Assert.Contains("ib-toast-bottom-end", region.ClassName);

        var toast = cut.FindAll(".ib-toast").Single(t => t.ParentElement?.TagName != "TEMPLATE" && t.Closest("template") is null);
        Assert.Contains("ib-alert-success", toast.ClassName);
        Assert.Null(toast.GetAttribute("role"));
        Assert.Contains("Saved it.", toast.TextContent);
        Assert.Equal(5, cut.FindAll("template").Count);
        Assert.Equal("polite", cut.Find(".ib-toast-live").GetAttribute("aria-live"));
    }

    [Fact]
    public void Toasts_shown_after_the_region_rendered_still_appear()
    {
        var cut = Render<ToastRegion>();

        _toasts.Error("Nope.");

        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll(".ib-toast"), t => t.Closest("template") is null && t.TextContent.Contains("Nope.")));
    }

    [Fact]
    public void Announcer_messages_from_a_static_response_are_rendered_for_the_script_to_read()
    {
        _announcer.Announce("Saved.");
        var cut = Render<AnnouncerRegion>();

        _announcer.Announce("Couldn't send.", assertive: true);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-ib-announcements] > [data-id]").Count));
        var messages = cut.FindAll("[data-ib-announcements] > [data-id]");
        Assert.True(cut.Find("[data-ib-announcements]").HasAttribute("hidden"));
        Assert.Equal("Saved.", messages[0].TextContent);
        Assert.False(messages[0].HasAttribute("data-assertive"));
        Assert.True(messages[1].HasAttribute("data-assertive"));
        Assert.NotEqual(messages[0].GetAttribute("data-id"), messages[1].GetAttribute("data-id"));
    }

    [Fact]
    public void A_layout_with_toasts_and_an_announcer_region_renders_each_message_once()
    {
        _announcer.Announce("Saved.");

        var toasts = Render<ToastRegion>();
        var region = Render<AnnouncerRegion>();

        Assert.Single(toasts.FindAll("[data-ib-announcements] > [data-id]"));
        Assert.Empty(region.FindAll("[data-ib-announcements] > [data-id]"));
    }

    [Theory]
    [InlineData(false, "status")]
    [InlineData(true, "alert")]
    public void Live_region_is_hidden_text_with_its_role(bool assertive, string role)
    {
        var region = Render<LiveRegion>(p => p.Add(x => x.Text, "12 results").Add(x => x.Assertive, assertive)).Find("div");

        Assert.Equal(role, region.GetAttribute("role"));
        Assert.Null(region.GetAttribute("aria-live"));
        Assert.Equal("true", region.GetAttribute("aria-atomic"));
        Assert.Contains("ib-visually-hidden", region.ClassName);
        Assert.Equal("12 results", region.TextContent);
    }

    [Fact]
    public void Progress_with_value_reports_aria_values_and_width()
    {
        var cut = Render<Progress>(p => p.Add(x => x.Value, 3).Add(x => x.Max, 5).Add(x => x.Label, "Files").Add(x => x.ValueText, "3 of 5 files"));
        var bar = cut.Find("[role=progressbar]");

        Assert.Equal("0", bar.GetAttribute("aria-valuemin"));
        Assert.Equal("5", bar.GetAttribute("aria-valuemax"));
        Assert.Equal("3", bar.GetAttribute("aria-valuenow"));
        Assert.Equal("3 of 5 files", bar.GetAttribute("aria-valuetext"));
        Assert.Equal(cut.Find(".ib-progress-label").Id, bar.GetAttribute("aria-labelledby"));
        Assert.Equal("width:60%", cut.Find(".ib-progress-bar").GetAttribute("style"));
        Assert.Equal("3 of 5 files", cut.Find(".ib-progress-value").TextContent);
    }

    [Fact]
    public void Progress_circle_fills_to_the_value_with_the_value_inside()
    {
        var cut = Render<ProgressCircle>(p => p.Add(x => x.Value, 72).Add(x => x.Label, "Profile").Add(x => x.ShowValue, true));
        var ring = cut.Find("[role=progressbar]");

        Assert.Equal("72", ring.GetAttribute("aria-valuenow"));
        Assert.Equal(cut.Find(".ib-progress-label").Id, ring.GetAttribute("aria-labelledby"));
        Assert.Equal("stroke-dashoffset:28", cut.Find(".ib-progress-ring-fill").GetAttribute("style"));
        Assert.Equal("72%", ring.QuerySelector(".ib-progress-ring-value")!.TextContent);
    }
}
