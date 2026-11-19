using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

/// <summary>Long content cut to its first lines, with a button to show the rest.</summary>
public class ShowMoreTests : BunitContext
{
    [Fact]
    public void The_content_is_all_there_and_the_button_waits_hidden_for_the_script()
    {
        SetRendererInfo(new RendererInfo("Static", false));
        var more = Render<ShowMore>(p => p.Add(s => s.Lines, 3).AddChildContent("A long background."));

        var root = more.Find("ib-show-more");
        Assert.Contains("--ib-show-more-lines:3", root.GetAttribute("style")!.Replace(" ", ""));
        var content = root.QuerySelector(".ib-show-more-content")!;
        Assert.Equal("A long background.", content.TextContent);
        var button = root.QuerySelector("button.ib-show-more-button")!;
        Assert.True(button.HasAttribute("hidden"));
        Assert.Equal("false", button.GetAttribute("aria-expanded"));
        Assert.Equal(content.Id, button.GetAttribute("aria-controls"));
        Assert.Equal(["Show more", "Show less"], button.QuerySelectorAll(".ib-show-more-more, .ib-show-more-less").Select(s => s.TextContent));
    }
}
