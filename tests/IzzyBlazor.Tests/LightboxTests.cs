namespace IzzyBlazor.Tests;

public class LightboxTests : BunitContext
{
    public LightboxTests() => SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Static", isInteractive: false));

    [Fact]
    public void Each_item_is_a_link_to_its_image_and_a_slide_of_the_viewer_wherever_it_sits()
    {
        // The items sit inside a layout, so they reach the lightbox after its own first render.
        var cut = Render<Lightbox>(p => p
            .Add(l => l.Label, "Product photos")
            .Add(l => l.Counter, true)
            .Add(l => l.Nav, LightboxNav.Thumbnails)
            .AddChildContent<Grid>(grid => grid
                .AddChildContent<LightboxItem>(item => item
                    .Add(x => x.Src, "front.jpg").Add(x => x.Thumbnail, "front-small.jpg").Add(x => x.Alt, "The kettle from the front").Add(x => x.Caption, "Front"))
                .AddChildContent<LightboxItem>(item => item
                    .Add(x => x.Src, "side.jpg").Add(x => x.DarkSrc, "side-dark.jpg").Add(x => x.Alt, "The kettle from the side"))));

        // With JavaScript off the links work on their own: each goes to its full image.
        var links = cut.FindAll("a.ib-lightbox-link");
        Assert.Equal(["front.jpg", "side.jpg"], links.Select(a => a.GetAttribute("href")));
        Assert.Equal("front-small.jpg", links[0].QuerySelector("img")!.GetAttribute("src"));
        Assert.Equal("The kettle from the front", links[0].QuerySelector("img")!.GetAttribute("alt"));

        // The viewer: a dialog named by Label, a slide per item, in order.
        var viewer = cut.Find("dialog.ib-lightbox-panel");
        Assert.Equal("Product photos", viewer.GetAttribute("aria-label"));
        Assert.False(viewer.HasAttribute("open"));
        var slides = viewer.QuerySelectorAll(".ib-carousel-item");
        Assert.Equal(2, slides.Length);
        Assert.Equal("front.jpg", slides[0].QuerySelector("img")!.GetAttribute("src"));
        Assert.Equal("Front", slides[0].QuerySelector("figcaption")!.TextContent);
        Assert.Equal(["side.jpg", "side-dark.jpg"], slides[1].QuerySelectorAll("img").Select(i => i.GetAttribute("src")));
        Assert.Equal("1 of 2", viewer.QuerySelector(".ib-lightbox-counter")!.GetAttribute("data-ib-text"));
        Assert.Equal(2, viewer.QuerySelectorAll(".ib-lightbox-thumb").Length);
    }
}
