namespace IzzyBlazor.Tests;

public class CarouselRenderTests : BunitContext
{
    private IRenderedComponent<Carousel> Trips(int index = 0, bool loop = false) => Render<Carousel>(p => p
        .Add(c => c.Label, "Featured trips")
        .Add(c => c.Index, index)
        .Add(c => c.Loop, loop)
        .Add(c => c.ItemsPerView, new Responsive<int> { Base = 1, Md = 3 })
        .AddChildContent<CarouselItem>(item => item.AddChildContent("Lisbon"))
        .AddChildContent<CarouselItem>(item => item.Add(x => x.Label, "Kyoto").AddChildContent("Kyoto"))
        .AddChildContent<CarouselItem>(item => item.AddChildContent("Quito")));

    [Fact]
    public void Renders_a_named_region_of_slides_with_buttons_and_a_dot_per_slide()
    {
        var cut = Trips(index: 1);

        var root = cut.Find("ib-carousel");
        Assert.Equal("region", root.GetAttribute("role"));
        Assert.Equal("carousel", root.GetAttribute("aria-roledescription"));
        Assert.Equal("Featured trips", root.GetAttribute("aria-label"));
        Assert.Equal("1", root.GetAttribute("data-ib-index"));
        Assert.Contains("ib-carousel-per-md", root.ClassName);
        Assert.Contains("--ib-carousel-per:1;--ib-carousel-per-md:3;", root.GetAttribute("style"));

        // Slides are groups named by place, or by their own label.
        var slides = cut.FindAll(".ib-carousel-track > .ib-carousel-item");
        Assert.Equal(["1 of 3", "Kyoto", "3 of 3"], slides.Select(s => s.GetAttribute("aria-label")));
        Assert.All(slides, s => Assert.Equal("slide", s.GetAttribute("aria-roledescription")));

        // The buttons and dots control the track; the dot for Index is current.
        var track = cut.Find(".ib-carousel-track").Id;
        Assert.Equal(track, cut.Find(".ib-carousel-prev").GetAttribute("aria-controls"));
        var dots = cut.FindAll(".ib-carousel-dot");
        Assert.Equal(["Slide 1", "Slide 2", "Slide 3"], dots.Select(d => d.GetAttribute("aria-label")));
        Assert.Equal([null, "true", null], dots.Select(d => d.GetAttribute("aria-current")));
        Assert.Null(cut.Find(".ib-carousel-prev").GetAttribute("aria-disabled"));
    }

    [Fact]
    public void Previous_starts_off_on_the_first_slide_unless_it_loops()
    {
        Assert.Equal("true", Trips().Find(".ib-carousel-prev").GetAttribute("aria-disabled"));
        Assert.Null(Trips(loop: true).Find(".ib-carousel-prev").GetAttribute("aria-disabled"));
    }

    [Fact]
    public void The_dots_sit_on_the_slide_when_one_shows_unless_placed_below()
    {
        Assert.DoesNotContain("ib-carousel-single", Trips().Find("ib-carousel").ClassName);
        var one = Render<Carousel>(p => p.Add(c => c.Label, "Trips").AddChildContent<CarouselItem>());
        Assert.Contains("ib-carousel-single", one.Find("ib-carousel").ClassName);
        var below = Render<Carousel>(p => p.Add(c => c.Label, "Trips").Add(c => c.Dots, CarouselDots.Below).AddChildContent<CarouselItem>());
        Assert.DoesNotContain("ib-carousel-single", below.Find("ib-carousel").ClassName);
    }

    [Fact]
    public void Autoplay_puts_its_button_first_and_a_fade_shows_the_starting_slide()
    {
        var cut = Render<Carousel>(p => p
            .Add(c => c.Label, "Quotes")
            .Add(c => c.Autoplay, true)
            .Add(c => c.Interval, TimeSpan.FromSeconds(6))
            .Add(c => c.Transition, CarouselTransition.Fade)
            .Add(c => c.Index, 1)
            .AddChildContent<CarouselItem>()
            .AddChildContent<CarouselItem>(item => item.Add(x => x.Interval, TimeSpan.FromSeconds(10))));

        var root = cut.Find("ib-carousel");
        Assert.Equal("6000", root.GetAttribute("data-ib-interval"));
        Assert.Contains("ib-carousel-fade", root.ClassName);

        // The rotation button comes before the other buttons, stopped until the script starts it.
        var buttons = cut.FindAll("ib-carousel > button");
        Assert.Contains("ib-carousel-rotate", buttons[0].ClassName);
        Assert.Equal("Start slide rotation", buttons[0].GetAttribute("aria-label"));
        Assert.Equal("Stop slide rotation", buttons[0].GetAttribute("data-ib-stop-label"));

        var slides = cut.FindAll(".ib-carousel-item");
        Assert.Equal([false, true], slides.Select(s => s.HasAttribute("data-ib-shown")));
        Assert.Equal("10000", slides[1].GetAttribute("data-ib-interval"));
        Assert.Equal("polite", cut.Find("[data-ib-live]").GetAttribute("aria-live"));
    }

    [Fact]
    public void Needs_a_label() =>
        Assert.Throws<InvalidOperationException>(() => Render<Carousel>(p => p.AddChildContent<CarouselItem>()));
}
