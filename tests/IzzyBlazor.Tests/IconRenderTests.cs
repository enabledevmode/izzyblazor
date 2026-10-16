namespace IzzyBlazor.Tests;

public class IconRenderTests : BunitContext
{
    [Fact]
    public void Decorative_icon_is_hidden_from_assistive_technology()
    {
        var svg = Render<Icon>(p => p.Add(i => i.Name, Hugeicons.Cancel01)).Find("svg");

        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        Assert.Null(svg.GetAttribute("role"));
        Assert.Equal("0 0 24 24", svg.GetAttribute("viewBox"));
        Assert.Equal("currentColor", svg.GetAttribute("stroke"));
        Assert.Equal("none", svg.GetAttribute("fill"));
        Assert.Equal("1.5", svg.GetAttribute("stroke-width"));
        Assert.Single(svg.QuerySelectorAll("path"));
    }

    [Fact]
    public void Labeled_icon_is_an_image_with_a_name()
    {
        var svg = Render<Icon>(p => p
            .Add(i => i.Name, Hugeicons.CircleCheck)
            .Add(i => i.Label, "Done")).Find("svg");

        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Equal("Done", svg.GetAttribute("aria-label"));
        Assert.Null(svg.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Role_resolves_through_the_current_icon_set()
    {
        var byRole = Render<Icon>(p => p.Add(i => i.Role, IconRole.Close));
        var byName = Render<Icon>(p => p.Add(i => i.Name, Hugeicons.Cancel01));

        byRole.MarkupMatches(byName.Markup);
    }

    [Fact]
    public void Size_color_stroke_and_spin()
    {
        var svg = Render<Icon>(p => p
            .Add(i => i.Name, Hugeicons.Favourite)
            .Add(i => i.Size, IconSize.Lg)
            .Add(i => i.Color, Color.DangerText)
            .Add(i => i.StrokeWidth, 2)
            .Add(i => i.Spin, true)).Find("svg");

        Assert.Equal("ib-icon ib-icon-lg ib-icon-spin ib-fg", svg.ClassName);
        Assert.Equal("--ib-fg:var(--ib-color-danger-text);", svg.GetAttribute("style"));
        Assert.Equal("2", svg.GetAttribute("stroke-width"));
    }

    [Fact]
    public void Filled_icons_use_fill_and_no_stroke()
    {
        var filled = new IconData("0 0 16 16", "<path d=\"M0 0h16v16H0z\"/>", IconStyle.Fill);
        var svg = Render<Icon>(p => p.Add(i => i.Name, filled)).Find("svg");

        Assert.Equal("currentColor", svg.GetAttribute("fill"));
        Assert.Null(svg.GetAttribute("stroke"));
    }

    [Fact]
    public void Name_or_role_is_required_but_not_both()
    {
        Assert.Throws<InvalidOperationException>(() => Render<Icon>());
        Assert.Throws<InvalidOperationException>(() => Render<Icon>(p => p
            .Add(i => i.Name, Hugeicons.Cancel01)
            .Add(i => i.Role, IconRole.Close)));
    }

    [Fact]
    public void Every_role_has_an_icon()
    {
        foreach (var role in Enum.GetValues<IconRole>())
        {
            Assert.False(string.IsNullOrEmpty(IconSet.Current.Get(role).Body), $"{role} has no icon");
        }
    }

    [Theory]
    [InlineData(typeof(Hugeicons), 6000)]
    [InlineData(typeof(Brands), 13)]
    public void Every_bundled_icon_passes_the_markup_check(Type set, int atLeast)
    {
        var icons = set.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(IconData))
            .Select(p => (IconData)p.GetValue(null)!)
            .ToList();

        Assert.True(icons.Count >= atLeast, $"{set.Name} has {icons.Count} icons");
        Assert.All(icons, icon => Assert.Null(IconMarkup.Find(icon.Body)));
    }

    [Fact]
    public void Brands_are_outlines_like_the_rest_of_the_set()
    {
        var brands = typeof(Brands).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(p => (IconData)p.GetValue(null)!);

        Assert.All(brands, icon => Assert.Equal(IconStyle.Stroke, icon.Style));
    }

    [Fact]
    public void Hugeicons_leave_the_stroke_to_the_svg_so_StrokeWidth_applies()
    {
        // Stroke color, round caps and joins and the 1.5 width come from <svg>; an element only
        // says so when it differs.
        Assert.DoesNotContain("stroke-width=\"1.5\"", Hugeicons.Delete02.Body);
        Assert.DoesNotContain("currentColor", Hugeicons.Delete02.Body);
        Assert.Equal(1.5, Hugeicons.Delete02.StrokeWidth);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<path d=\"M0 0\" onclick=\"alert(1)\"/>")]
    [InlineData("<foreignObject><div>hi</div></foreignObject>")]
    [InlineData("<use href=\"#x\"/>")]
    [InlineData("<use xmlns:xlink=\"http://www.w3.org/1999/xlink\" xlink:href=\"#x\"/>")]
    [InlineData("<image href=\"https://example.com/a.png\"/>")]
    [InlineData("<a href=\"javascript:alert(1)\"><path d=\"M0 0\"/></a>")]
    [InlineData("<animate attributeName=\"href\" to=\"javascript:alert(1)\"/>")]
    [InlineData("<path d=\"M0 0\" style=\"fill:url(javascript:alert(1))\"/>")]
    [InlineData("<path d=\"M0 0\"")]
    [InlineData("<!--><img src=x onerror=alert(1)>-->")]
    [InlineData("<!---><img src=x onerror=alert(1)>-->")]
    [InlineData("<title><g><![CDATA[><img src=x onerror=alert(1)>]]></g></title>")]
    [InlineData("<g><![CDATA[x]]></g>")]
    [InlineData("<g>loose text</g>")]
    [InlineData("<path d=\"M0 0\" fill=\"url(https://example.com/a.svg#p)\"/>")]
    public void Unsafe_or_broken_icon_markup_is_rejected(string body) =>
        Assert.NotNull(IconMarkup.Find(body));

    [Theory]
    [InlineData("<title>Settings</title><path d=\"M0 0h24\"/>")]
    [InlineData("<defs><linearGradient id=\"g\"><stop offset=\"0\"/></linearGradient></defs><path d=\"M0 0\" fill=\"url(#g)\"/>")]
    [InlineData("<path d=\"M0 0\" fill=\"url( '#g' )\"/>")]
    public void Plain_drawing_markup_is_allowed(string body) =>
        Assert.Null(IconMarkup.Find(body));

    [Fact]
    public void Icon_with_unsafe_markup_throws_naming_the_icon()
    {
        var bad = new IconData("0 0 24 24", "<script>alert(1)</script>", IconStyle.Fill, Name: "evil");

        var error = Assert.Throws<InvalidOperationException>(() => Render<Icon>(p => p.Add(i => i.Name, bad)));
        Assert.Contains("\"evil\"", error.Message);
        Assert.Contains("<script>", error.Message);
    }
}
