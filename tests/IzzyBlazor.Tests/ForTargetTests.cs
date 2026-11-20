using Bunit;

namespace IzzyBlazor.Tests;

/// <summary>A For that names one element takes its id with or without "#".</summary>
public sealed class ForTargetTests : BunitContext
{
    [Fact]
    public void A_dialog_trigger_takes_the_id_with_or_without_a_hash()
    {
        var bare = Render<DialogTrigger>(p => p.Add(x => x.For, "invite").AddChildContent("Invite"));
        var hash = Render<DialogTrigger>(p => p.Add(x => x.For, "#invite").AddChildContent("Invite"));

        Assert.Equal("invite", bare.Find("button").GetAttribute("commandfor"));
        Assert.Equal("invite", hash.Find("button").GetAttribute("commandfor"));
    }

    [Fact]
    public void A_selector_that_is_not_an_id_says_so()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render<DialogTrigger>(p => p.Add(x => x.For, ".invite").AddChildContent("Invite")));

        Assert.Contains("DialogTrigger's For names one element by its id", error.Message);
    }
}
