namespace IzzyBlazor.Tests;

public class IconRenderTests : BunitContext
{
    [Fact]
    public void Decorative_icon_is_hidden_from_assistive_technology()
    {
        var svg = Render<Icon>(p => p.Add(i => i.Name, Lucide.X)).Find("svg");

        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        Assert.Null(svg.GetAttribute("role"));
        Assert.Equal("0 0 24 24", svg.GetAttribute("viewBox"));
        Assert.Equal("currentColor", svg.GetAttribute("stroke"));
        Assert.Equal("none", svg.GetAttribute("fill"));
        Assert.Equal(2, svg.QuerySelectorAll("path").Length);
    }

    [Fact]
    public void Labelled_icon_is_an_image_with_a_name()
    {
        var svg = Render<Icon>(p => p
            .Add(i => i.Name, Lucide.CircleCheck)
            .Add(i => i.Label, "Done")).Find("svg");

        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Equal("Done", svg.GetAttribute("aria-label"));
        Assert.Null(svg.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Role_resolves_through_the_current_icon_set()
    {
        var byRole = Render<Icon>(p => p.Add(i => i.Role, IconRole.Close));
        var byName = Render<Icon>(p => p.Add(i => i.Name, Lucide.X));

        byRole.MarkupMatches(byName.Markup);
    }

    [Fact]
    public void Size_color_stroke_and_spin()
    {
        var svg = Render<Icon>(p => p
            .Add(i => i.Name, Lucide.Heart)
            .Add(i => i.Size, IconSize.Lg)
            .Add(i => i.Color, Color.DangerText)
            .Add(i => i.StrokeWidth, 1.5)
            .Add(i => i.Spin, true)).Find("svg");

        Assert.Equal("ib-icon ib-icon-lg ib-icon-spin ib-fg", svg.ClassName);
        Assert.Equal("--ib-fg:var(--ib-color-danger-text);", svg.GetAttribute("style"));
        Assert.Equal("1.5", svg.GetAttribute("stroke-width"));
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
            .Add(i => i.Name, Lucide.X)
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

    [Fact]
    public void Every_lucide_icon_passes_the_markup_check()
    {
        var icons = typeof(Lucide).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(IconData))
            .Select(p => (IconData)p.GetValue(null)!)
            .ToList();

        Assert.True(icons.Count > 1000);
        Assert.All(icons, icon => Assert.Null(IconMarkup.Find(icon.Body)));
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
    public void Unsafe_or_broken_icon_markup_is_rejected(string body) =>
        Assert.NotNull(IconMarkup.Find(body));

    [Fact]
    public void Icon_with_unsafe_markup_throws_naming_the_icon()
    {
        var bad = new IconData("0 0 24 24", "<script>alert(1)</script>", IconStyle.Fill, Name: "evil");

        var error = Assert.Throws<InvalidOperationException>(() => Render<Icon>(p => p.Add(i => i.Name, bad)));
        Assert.Contains("\"evil\"", error.Message);
        Assert.Contains("<script>", error.Message);
    }
}
