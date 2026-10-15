using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class AvatarRenderTests : BunitContext
{
    public AvatarRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    [Theory]
    [InlineData("Grace Hopper", "GH")]
    [InlineData("grace brewster murray hopper", "GH")]
    [InlineData("Linus", "L")]
    [InlineData("  Ada   Lovelace ", "AL")]
    [InlineData("(Acme) Corp", "C")]
    [InlineData("", "")]
    public void Initials_come_from_first_and_last_words(string name, string expected) =>
        Assert.Equal(expected, Avatar.InitialsOf(name));

    [Fact]
    public void Hue_is_stable_and_ignores_case_and_spacing() =>
        Assert.Equal(Avatar.HueOf("Grace Hopper"), Avatar.HueOf(" grace hopper "));

    [Fact]
    public void Named_avatar_is_an_image_with_initials_and_a_hue()
    {
        var avatar = Render<Avatar>(p => p.Add(a => a.Name, "Grace Hopper")).Find(".ib-avatar");

        Assert.Equal("img", avatar.GetAttribute("role"));
        Assert.Equal("Grace Hopper", avatar.GetAttribute("aria-label"));
        Assert.Equal("GH", avatar.QuerySelector(".ib-avatar-fallback")!.TextContent);
        Assert.Contains($"--ib-avatar-hue:{Avatar.HueOf("Grace Hopper")}", avatar.GetAttribute("style"));
        Assert.Null(avatar.QuerySelector(".ib-avatar-img"));
    }

    [Fact]
    public void Photo_is_a_background_over_the_initials_with_the_url_escaped()
    {
        var avatar = Render<Avatar>(p => p.Add(a => a.Name, "Grace Hopper").Add(a => a.Src, "/img/a\"b.jpg")).Find(".ib-avatar");

        Assert.Equal("GH", avatar.QuerySelector(".ib-avatar-fallback")!.TextContent);
        Assert.Equal("background-image:url(\"/img/a\\\"b.jpg\")", avatar.QuerySelector(".ib-avatar-img")!.GetAttribute("style"));
        Assert.Empty(avatar.QuerySelectorAll("img"));
    }

    [Fact]
    public void No_name_shows_an_icon_and_is_hidden_from_screen_readers()
    {
        var avatar = Render<Avatar>().Find(".ib-avatar");

        Assert.Equal("true", avatar.GetAttribute("aria-hidden"));
        Assert.Contains("ib-avatar-anonymous", avatar.ClassName);
        Assert.NotNull(avatar.QuerySelector(".ib-avatar-fallback svg"));
    }

    [Fact]
    public void Status_dot_joins_the_name_and_decorative_hides_it()
    {
        var online = Render<Avatar>(p => p
            .Add(a => a.Name, "Grace Hopper")
            .Add(a => a.Status, Status.Success)
            .Add(a => a.StatusLabel, "Online")
            .Add(a => a.Size, AvatarSize.Lg)
            .Add(a => a.Square, true)).Find(".ib-avatar");

        Assert.Equal("Grace Hopper (Online)", online.GetAttribute("aria-label"));
        Assert.NotNull(online.QuerySelector(".ib-avatar-status.ib-avatar-status-success"));
        Assert.Contains("ib-avatar-lg", online.ClassName);
        Assert.Contains("ib-avatar-square", online.ClassName);

        var decorative = Render<Avatar>(p => p.Add(a => a.Name, "Grace Hopper").Add(a => a.Decorative, true)).Find(".ib-avatar");
        Assert.Equal("true", decorative.GetAttribute("aria-hidden"));
        Assert.Null(decorative.GetAttribute("role"));
    }

    [Fact]
    public void Accent_fills_the_fallback_and_sets_its_text()
    {
        var avatar = Render<Avatar>(p => p.Add(a => a.Name, "Acme").Add(a => a.Accent, Accent.Primary)).Find(".ib-avatar");

        Assert.Contains("ib-avatar-colored", avatar.ClassName);
        Assert.Contains("--ib-accent:var(--ib-color-primary)", avatar.GetAttribute("style"));
        Assert.Contains("--ib-on-accent:var(--ib-color-on-primary)", avatar.GetAttribute("style"));
    }

    [Fact]
    public void Group_counts_the_rest_past_max_and_sizes_its_avatars()
    {
        var cut = Render<AvatarGroup>(p => p
            .Add(g => g.Max, 2)
            .Add(g => g.Size, AvatarSize.Sm)
            .Add(g => g.Label, "Members")
            .AddChildContent(b =>
            {
                foreach (var name in new[] { "Grace Hopper", "Alan Turing", "Ada Lovelace", "Linus" })
                {
                    b.OpenComponent<Avatar>(0);
                    b.AddComponentParameter(1, nameof(Avatar.Name), name);
                    b.CloseComponent();
                }
            }));

        var group = cut.Find(".ib-avatar-group");
        Assert.Equal("group", group.GetAttribute("role"));
        Assert.Equal("Members", group.GetAttribute("aria-label"));
        Assert.Equal("2", group.GetAttribute("data-max"));
        Assert.All(cut.FindAll(".ib-avatar"), a => Assert.Contains("ib-avatar-sm", a.ClassName));

        var more = cut.Find(".ib-avatar-more");
        Assert.Equal("+2", more.TextContent);
        Assert.Equal("2 more", more.GetAttribute("aria-label"));
        Assert.Contains("ib-avatar-more", cut.FindAll(".ib-avatar")[^1].ClassName);
    }

    [Fact]
    public void Group_within_max_has_no_more_avatar()
    {
        var cut = Render<AvatarGroup>(p => p
            .Add(g => g.Max, 5)
            .AddChildContent<Avatar>(a => a.Add(x => x.Name, "Grace Hopper")));

        Assert.Empty(cut.FindAll(".ib-avatar-more"));
    }
}
