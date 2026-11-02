using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class AvatarRenderTests : BunitContext
{
    public AvatarRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    [Theory]
    [InlineData("Grace Hopper", "GH")]
    [InlineData("Linus", "L")]
    [InlineData("  Ada   Lovelace ", "AL")]
    public void Initials_come_from_first_and_last_words(string name, string expected) =>
        Assert.Equal(expected, Avatar.InitialsOf(name));

    [Fact]
    public void Photo_is_a_background_over_the_initials_with_the_url_escaped()
    {
        var avatar = Render<Avatar>(p => p.Add(a => a.Name, "Grace Hopper").Add(a => a.Src, "/img/a\"b.jpg")).Find(".ib-avatar");

        Assert.Equal("GH", avatar.QuerySelector(".ib-avatar-fallback")!.TextContent);
        Assert.Equal("background-image:url(\"/img/a\\\"b.jpg\")", avatar.QuerySelector(".ib-avatar-img")!.GetAttribute("style"));
        Assert.Empty(avatar.QuerySelectorAll("img"));
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

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "izzyblazor.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("izzyblazor.sln not found.");
    }
}
