using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class DisclosureRenderTests : BunitContext
{
    public DisclosureRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    [Fact]
    public void Static_pages_render_open_with_nothing_for_the_script()
    {
        var details = Render<AccordionItem>(p => p.Add(i => i.Title, "Notes").Add(i => i.Open, true)).Find("details");

        Assert.True(details.HasAttribute("open"));
        Assert.False(details.HasAttribute("data-open"));
        Assert.False(details.HasAttribute("data-ib-seq"));
        Assert.False(details.HasAttribute("data-ib-interactive"));
    }

    private static RenderFragment Items(params (string Id, string Title, bool Open, bool Disabled)[] items) => builder =>
    {
        foreach (var (id, title, open, disabled) in items)
        {
            builder.OpenComponent<AccordionItem>(0);
            builder.AddComponentParameter(1, nameof(AccordionItem.Id), id);
            builder.AddComponentParameter(2, nameof(AccordionItem.Title), title);
            builder.AddComponentParameter(3, nameof(AccordionItem.Open), open);
            builder.AddComponentParameter(4, nameof(AccordionItem.Disabled), disabled);
            builder.CloseComponent();
        }
    };

    [Fact]
    public void Toggle_event_updates_bound_state()
    {
        bool? reported = null;
        var cut = Render<AccordionItem>(p => p
            .Add(i => i.Id, "notes")
            .Add(i => i.Title, "Notes")
            .Add(i => i.OpenChanged, (bool open) => reported = open));

        cut.Find("details").TriggerEvent("onibtoggle", new IzzyToggleEventArgs { Id = "notes", Open = true });

        Assert.True(reported);
        Assert.True(cut.Find("details").HasAttribute("open"));
    }

    [Fact]
    public void Toggle_event_from_a_nested_element_is_ignored()
    {
        bool? reported = null;
        var cut = Render<AccordionItem>(p => p
            .Add(i => i.Id, "outer")
            .Add(i => i.Title, "Outer")
            .Add(i => i.OpenChanged, (bool open) => reported = open));

        cut.Find("details").TriggerEvent("onibtoggle", new IzzyToggleEventArgs { Id = "inner", Open = true });

        Assert.Null(reported);
        Assert.False(cut.Find("details").HasAttribute("open"));
    }
}

/// <summary>
/// On interactive pages the script opens and closes a Disclosure to match data-open, and
/// data-ib-seq says which of the browser's toggles C# has handled (toggle.ts).
/// </summary>
public class InteractiveDisclosureRenderTests : BunitContext
{
    public InteractiveDisclosureRenderTests() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));
}
