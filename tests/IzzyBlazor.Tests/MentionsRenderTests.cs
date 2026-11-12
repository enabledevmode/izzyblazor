using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class MentionsRenderTests : BunitContext
{
    public class Model
    {
        public string? Reply { get; set; }
    }

    private static readonly string[] People = ["aisha.bello", "daniel.kim", "mei.chen"];
    private static readonly string[] Issues = ["1042", "1057", "1063"];

    private readonly Model _model = new();
    private readonly List<MentionQuery> _searches = [];

    public MentionsRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private Task<IEnumerable<string>> Find(MentionQuery query, CancellationToken token)
    {
        _searches.Add(query);
        var source = query.Prefix == "#" ? Issues : People;
        return Task.FromResult(source.Where(s => s.Contains(query.Text, StringComparison.OrdinalIgnoreCase)));
    }

    private IRenderedComponent<Mentions<string>> Reply() => Render<Mentions<string>>(p => p
        .AddCascadingValue(new EditContext(_model))
        .Add(c => c.Value, _model.Reply)
        .Add(c => c.ValueExpression, () => _model.Reply)
        .Add(c => c.Prefixes, ["@", "#"])
        .Add(c => c.Search, Find));

    [Fact]
    public void Each_trigger_gets_its_first_suggestions_and_the_text_area_points_at_them()
    {
        var cut = Reply();

        Assert.Equal([new MentionQuery("@", ""), new MentionQuery("#", "")], _searches);
        var area = cut.Find("textarea");
        Assert.Equal("list", area.GetAttribute("aria-autocomplete"));
        var list = cut.Find("#" + area.GetAttribute("aria-controls"));
        Assert.Equal("listbox", list.GetAttribute("role"));
        Assert.Equal(["@aisha.bello", "@daniel.kim", "@mei.chen", "#1042", "#1057", "#1063"],
            list.QuerySelectorAll("[role=option]").Select(o => o.GetAttribute("data-prefix") + o.GetAttribute("data-text")));
    }

    [Fact]
    public void A_static_page_fetched_for_a_trigger_answers_with_that_triggers_suggestions()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("reply?ib-suggest=_model.Reply&ib-suggest-q=%2306");
        var cut = Reply();

        Assert.Equal([new MentionQuery("#", "06")], _searches);
        var list = cut.Find("[role=listbox]");
        Assert.True(list.HasAttribute("data-ib-answered"));
        Assert.Equal(["1063"], list.QuerySelectorAll("[role=option]").Select(o => o.GetAttribute("data-text")));
    }

    [Fact]
    public void Saved_text_shows_its_mentions_linked_and_skips_email_addresses()
    {
        const string text = "Thanks @daniel.kim! Mail ops@example.com, then #1063.";
        Assert.Equal([new Mention("@", "daniel.kim", 7), new Mention("#", "1063", 47)], Mention.Parse(text, "@", "#"));

        var cut = Render<MentionText>(p => p
            .Add(c => c.Text, text)
            .Add(c => c.Href, m => m.Prefix == "@" ? $"/people/{m.Text}" : null));

        Assert.Equal(text, cut.Find(".ib-mention-text").TextContent);
        var link = cut.Find("a.ib-mention");
        Assert.Equal(("/people/daniel.kim", "@daniel.kim"), (link.GetAttribute("href"), link.TextContent));
        Assert.Single(cut.FindAll(".ib-mention")); // # isn't a trigger here
    }
}
