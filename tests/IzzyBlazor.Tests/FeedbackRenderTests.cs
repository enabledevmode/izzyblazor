using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class FeedbackRenderTests : BunitContext
{
    // Services must be registered before anything starts the renderer, SetRendererInfo included.
    private readonly ToastService _toasts = new(new ServiceCollection().BuildServiceProvider());

    public FeedbackRenderTests()
    {
        Services.AddSingleton(_toasts);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    [Theory]
    [InlineData(Status.Info, "status")]
    [InlineData(Status.Success, "status")]
    [InlineData(Status.Warning, "alert")]
    [InlineData(Status.Danger, "alert")]
    public void Alert_role_follows_urgency(Status status, string role)
    {
        var alert = Render<Alert>(p => p.Add(a => a.Status, status).AddChildContent("Message")).Find(".ib-alert");

        Assert.Equal(role, alert.GetAttribute("role"));
        Assert.Contains($"ib-alert-{status.ToString().ToLowerInvariant()}", alert.ClassName);
        Assert.NotNull(alert.QuerySelector(".ib-alert-icon"));
    }

    [Fact]
    public void Alert_title_actions_and_hidden_icon()
    {
        var alert = Render<Alert>(p => p
            .Add(a => a.Title, "Saved")
            .Add(a => a.HideIcon, true)
            .Add(a => a.Actions, (RenderFragment)(b => b.AddContent(0, "Undo")))
            .AddChildContent("Done.")).Find(".ib-alert");

        Assert.Equal("Saved", alert.QuerySelector(".ib-alert-title")!.TextContent);
        Assert.Equal("Done.", alert.QuerySelector(".ib-alert-message")!.TextContent);
        Assert.Equal("Undo", alert.QuerySelector(".ib-alert-actions")!.TextContent);
        Assert.Null(alert.QuerySelector(".ib-alert-icon"));
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

        var cut = Render<ToastRegion>(p => p.Add(r => r.Duration, 3000));

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
    public void Spinner_is_a_status_with_hidden_label_and_icon_size()
    {
        var spinner = Render<Spinner>(p => p.Add(s => s.Size, IconSize.Lg).Add(s => s.Label, "Loading orders")).Find(".ib-spinner");

        Assert.Equal("status", spinner.GetAttribute("role"));
        Assert.Contains("ib-icon-lg", spinner.ClassName);
        Assert.Equal("Loading orders", spinner.QuerySelector(".ib-visually-hidden")!.TextContent);
        Assert.Equal("true", spinner.QuerySelector("svg")!.GetAttribute("aria-hidden"));
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
    public void Progress_clamps_and_without_value_is_indeterminate()
    {
        var over = Render<Progress>(p => p.Add(x => x.Value, 150).Add(x => x.ShowValue, true));
        Assert.Equal("100", over.Find("[role=progressbar]").GetAttribute("aria-valuenow"));
        Assert.Equal("100%", over.Find(".ib-progress-value").TextContent);

        var busy = Render<Progress>(p => p.Add(x => x.Label, "Working"));
        Assert.Contains("ib-progress-indeterminate", busy.Find(".ib-progress").ClassName);
        Assert.False(busy.Find("[role=progressbar]").HasAttribute("aria-valuenow"));
        Assert.Null(busy.Find(".ib-progress-bar").GetAttribute("style"));
    }

    [Fact]
    public void Skeleton_shapes_and_loaded_content()
    {
        var text = Render<Skeleton>(p => p.Add(s => s.Lines, 3)).Find(".ib-skeleton-text");
        Assert.Equal("true", text.GetAttribute("aria-hidden"));
        Assert.Equal(3, text.QuerySelectorAll(".ib-skeleton-line").Length);

        var block = Render<Skeleton>(p => p.Add(s => s.Height, Length.Rem(8))).Find(".ib-skeleton");
        Assert.Contains("ib-skeleton-block", block.ClassName);
        Assert.Contains("--ib-skeleton-h:8rem", block.GetAttribute("style"));

        Assert.Contains("ib-skeleton-circle", Render<Skeleton>(p => p.Add(s => s.Circle, true)).Find(".ib-skeleton").ClassName);

        var loaded = Render<Skeleton>(p => p.Add(s => s.Loaded, true).AddChildContent("<p>Ready</p>"));
        Assert.Empty(loaded.FindAll(".ib-skeleton, .ib-skeleton-text"));
        Assert.Equal("Ready", loaded.Find("p").TextContent);
    }

    [Fact]
    public void Static_toast_action_is_a_link_and_the_toast_lasts_twice_as_long()
    {
        _toasts.Info("Message archived.", action: new ToastAction("Undo", Href: "/inbox?restore=42") { OnClick = () => Task.CompletedTask });

        var cut = Render<ToastRegion>(p => p.Add(r => r.Duration, 4000));
        var toast = cut.FindAll(".ib-toast").Single(t => t.Closest("template") is null);

        var action = toast.QuerySelector("a.ib-toast-action")!;
        Assert.Equal("/inbox?restore=42", action.GetAttribute("href"));
        Assert.Equal("Undo", action.TextContent);
        Assert.Equal("8000", toast.GetAttribute("data-duration"));
    }

    [Fact]
    public void Static_toast_action_without_a_link_is_refused() =>
        Assert.Throws<InvalidOperationException>(() =>
            _toasts.Info("Archived.", action: new ToastAction("Undo") { OnClick = () => Task.CompletedTask }));

    [Fact]
    public void Toast_templates_have_an_action_slot_for_the_script()
    {
        var cut = Render<ToastRegion>();

        // A template's children live in its content fragment, out of reach of QuerySelector, so
        // count the slots in the markup: one per status template.
        Assert.Equal(5, cut.FindAll("template").Count);
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Count(cut.Markup, "data-ib-toast-action"));
    }

    [Fact]
    public void Toast_action_survives_json_without_its_callback()
    {
        var toast = new ToastMessage("Archived.", Action: new ToastAction("Undo", "/undo") { OnClick = () => Task.CompletedTask });

        var json = System.Text.Json.JsonSerializer.Serialize(toast);
        var back = System.Text.Json.JsonSerializer.Deserialize<ToastMessage>(json)!;

        Assert.DoesNotContain("OnClick", json);
        Assert.Equal(new ToastAction("Undo", "/undo"), back.Action);
    }
}
