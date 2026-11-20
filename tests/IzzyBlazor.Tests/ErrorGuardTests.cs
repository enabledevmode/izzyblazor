using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class ErrorGuardTests : BunitContext
{
    [Fact]
    public void A_part_that_throws_shows_the_alert_and_comes_back_on_try_again_and_on_navigation()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var report = new Report { Broken = true };
        Exception? seen = null;
        var cut = Render<ErrorGuard>(p => p
            .Add(g => g.OnError, EventCallback.Factory.Create<Exception>(this, e => seen = e))
            .AddChildContent<SalesReport>(r => r.Add(x => x.Report, report)));

        // The alert in its place, named, with Try again; the page goes on.
        var alert = cut.Find(".ib-error-guard .ib-alert");
        Assert.Contains("Something went wrong", alert.TextContent);
        Assert.Contains("This part of the page stopped working.", alert.TextContent);
        Assert.Equal("The report failed.", seen?.Message);
        Assert.Empty(cut.FindAll("details"));

        report.Broken = false;
        cut.Find("button").Click();
        Assert.Equal("Revenue: 1,200", cut.Find("p").TextContent);

        // Broken again, then a navigation clears it with no click.
        report.Broken = true;
        cut.Render(p => p.AddChildContent<SalesReport>(r => r.Add(x => x.Report, report)));
        Assert.NotEmpty(cut.FindAll(".ib-error-guard"));
        report.Broken = false;
        Services.GetRequiredService<NavigationManager>().NavigateTo("/elsewhere");
        cut.WaitForAssertion(() => Assert.Equal("Revenue: 1,200", cut.Find("p").TextContent));
    }

    [Fact]
    public void On_a_static_page_it_offers_a_full_reload_and_details_only_when_asked()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/reports?year=2026");
        var cut = Render<ErrorGuard>(p => p
            .Add(g => g.ShowDetails, true)
            .AddChildContent<SalesReport>(r => r.Add(x => x.Report, new Report { Broken = true })));

        // A button does nothing without a circuit, so the next step is a link to the same address.
        var reload = cut.Find("a.ib-btn");
        Assert.Equal("Reload", reload.TextContent.Trim());
        Assert.Equal("http://localhost/reports?year=2026", reload.GetAttribute("href"));
        Assert.Equal("false", reload.GetAttribute("data-enhance-nav"));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Try again"));
        Assert.Contains("The report failed.", cut.Find("details").TextContent);
    }

    [Fact]
    public void A_static_page_rendered_again_after_a_failure_shows_what_the_guard_caught_before()
    {
        // The server renders a failed page a second time; the guard that caught the exception the
        // first time shows its message without rendering its content again.
        var replay = new Replay();
        Services.AddSingleton<IErrorGuardReplay>(replay);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var report = new Report { Broken = true };

        Render<ErrorGuard>(p => p.AddChildContent<SalesReport>(r => r.Add(x => x.Report, report)));
        Assert.Equal("The report failed.", replay.Caught[0].Message);

        replay.Again = true;
        report.Broken = false;
        var again = Render<ErrorGuard>(p => p.Add(g => g.ShowDetails, true).AddChildContent<SalesReport>(r => r.Add(x => x.Report, report)));
        Assert.Contains("Something went wrong", again.Find(".ib-error-guard .ib-alert").TextContent);
        Assert.Contains("The report failed.", again.Find("details").TextContent);
        Assert.Empty(again.FindAll("p"));
    }

    private sealed class Replay : IErrorGuardReplay
    {
        public Dictionary<int, Exception> Caught { get; } = [];
        public bool Again { get; set; }
        public int Join() => 0;
        void IErrorGuardReplay.Caught(int index, Exception exception) => Caught[index] = exception;
        public Exception? CaughtBefore(int index) => Again ? Caught.GetValueOrDefault(index) : null;
    }

    private sealed class Report
    {
        public bool Broken { get; set; }
    }

    private sealed class SalesReport : ComponentBase
    {
        [Parameter] public Report Report { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (Report.Broken) throw new InvalidOperationException("The report failed.");
            builder.AddMarkupContent(0, "<p>Revenue: 1,200</p>");
        }
    }
}
