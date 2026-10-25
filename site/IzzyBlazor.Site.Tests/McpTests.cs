using System.Net;
using System.Text.RegularExpressions;
using IzzyBlazor.Site.Services;
using IzzyBlazor.Site.Services.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace IzzyBlazor.Site.Tests;

// The MCP server at /mcp and llms.txt: a real client gets the tools and answers, and every
// component, type, example and article answers as Markdown with no HTML or cref: left.
public partial class McpTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private McpDocs Docs => site.Services.GetRequiredService<McpDocs>();

    private async Task<McpClient> Connect() => await McpClient.CreateAsync(new HttpClientTransport(
        new HttpClientTransportOptions { Endpoint = new Uri(site.Client.BaseAddress!, "mcp") }));

    private static string TextOf(CallToolResult result) => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    [Fact]
    public async Task A_client_lists_the_read_only_tools_and_gets_answers()
    {
        await using var client = await Connect();
        var tools = await client.ListToolsAsync();
        Assert.Equal(["get_article", "get_component", "get_example", "get_type", "list_components", "search"], tools.Select(t => t.Name).Order());
        Assert.All(tools, t => Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint));

        var search = TextOf(await client.CallToolAsync("search", new Dictionary<string, object?> { ["query"] = "button" }));
        Assert.Contains("Component: **Button**", search.Split('\n').First(l => l.StartsWith("- ", StringComparison.Ordinal)));

        var button = await client.CallToolAsync("get_component", new Dictionary<string, object?> { ["name"] = "Button" });
        Assert.NotEqual(true, button.IsError);
        Assert.Contains("# Button", TextOf(button));
        Assert.Contains("| Variant |", TextOf(button));

        var unknown = await client.CallToolAsync("get_component", new Dictionary<string, object?> { ["name"] = "Buton" });
        Assert.True(unknown.IsError);
        Assert.Contains("Did you mean Button", TextOf(unknown));
    }

    [Fact]
    public void Every_component_and_part_answers_in_Markdown()
    {
        var wrong = new List<string>();
        foreach (var component in site.Api.File.Components.Where(c => c.Obsolete is null))
        {
            Check(wrong, component.Name, Docs.GetComponent(component.Name));
        }

        foreach (var name in site.Api.File.Types.Select(t => t.Name).Concat(site.Api.File.Bases.Select(b => b.Name)))
        {
            Check(wrong, $"type {name}", Docs.GetType(name));
        }

        Check(wrong, "list_components", Docs.ListComponents());
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    [Fact]
    public void Every_example_answers_with_its_source()
    {
        var wrong = new List<string>();
        foreach (var (component, example) in site.Registry.AllExamples)
        {
            var answer = Docs.GetExample(example.Id);
            if (!answer.Contains("```razor", StringComparison.Ordinal) || !answer.Contains(example.Title, StringComparison.Ordinal))
            {
                wrong.Add($"{example.Id}: no source or title");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    [Fact]
    public async Task Every_article_and_Overview_answers_as_Markdown_with_its_examples()
    {
        var wrong = new List<string>();
        foreach (var path in Articles.Sections.SelectMany(s => s.Articles.Select(s.HrefOf)).Concat(site.Registry.Components.Select(Registry.OverviewHref)))
        {
            var answer = await Docs.GetArticleAsync(path);
            Check(wrong, path, answer);
            if (!answer.Contains("\n# ", StringComparison.Ordinal) || answer.Length < 400)
            {
                wrong.Add($"{path}: no title or too short ({answer.Length})");
            }

            // An Overview's examples come as their source, not their previews' text.
            if (site.Registry.Components.FirstOrDefault(c => Registry.OverviewHref(c) == path) is { Examples: [var first, ..] }
                && !answer.Contains($"(`{first.Id}`)", StringComparison.Ordinal))
            {
                wrong.Add($"{path}: example {first.Id} not replaced by its source");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        Assert.Contains("(`form-columns`)", await Docs.GetArticleAsync("forms"));
        Assert.Contains("docs/components/button", await Docs.GetArticleAsync("Button"));
    }

    [Fact]
    public async Task Unknown_names_suggest_close_ones_and_the_sites_own_forms_resolve()
    {
        Assert.Contains("Did you mean Select", Assert.Throws<McpDocsException>(() => Docs.GetComponent("Selct")).Message);
        Assert.Contains("ControlSize", Assert.Throws<McpDocsException>(() => Docs.GetType("ControlSise")).Message);
        Assert.Contains("button-variants", Assert.Throws<McpDocsException>(() => Docs.GetExample("Button")).Message);
        Assert.Equal(Docs.GetComponent("Select"), Docs.GetComponent("<Select TValue=\"string\">"));
        Assert.Contains("form-signup", Docs.GetExample("https://izzyblazor.com/demos/form/sign-up"));
        Assert.Equal(Docs.GetExample("form-columns"), Docs.GetExample("ex-form-columns"));
        Assert.Contains("docs/troubleshooting/onclick", await Docs.GetArticleAsync("troubleshooting/onclick"));
    }

    [Fact]
    public void Long_or_missing_input_is_cut_or_refused_quickly()
    {
        var huge = new string('a', 1_000_000);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Assert.Throws<McpDocsException>(() => Docs.GetComponent(huge));
        Assert.Throws<McpDocsException>(() => Docs.GetType(huge));
        Assert.Throws<McpDocsException>(() => Docs.GetExample(huge));
        Assert.True(clock.ElapsedMilliseconds < 1000, $"{clock.ElapsedMilliseconds} ms");
        Assert.True(Assert.Throws<McpDocsException>(() => Docs.GetComponent(huge)).Message.Length < 500);
        Assert.StartsWith("Give", Assert.Throws<McpDocsException>(() => Docs.GetComponent(null!)).Message);
        Assert.StartsWith("Give", Assert.Throws<McpDocsException>(() => Docs.GetExample(" ")).Message);
    }

    [Fact]
    public void A_sentence_finds_the_entries_matching_most_of_its_words_and_each_result_names_its_call()
    {
        var answer = Docs.Search("a settings form that posts on a static page");
        Assert.Contains("nothing matches every word", answer);
        Assert.Contains("→ get_", answer);
        Assert.Contains("→ get_example(\"form-signup\")", Docs.Search("sign up"));
    }

    [Fact]
    public async Task The_endpoint_answers_with_its_own_statuses_not_the_sites_pages()
    {
        // No event stream or session to GET or DELETE.
        var get = await site.Client.GetAsync("mcp");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Assert.Contains("POST", get.Content.Headers.Allow);

        // A POST to another of the site's names keeps its method (308, not 301).
        using var noRedirect = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = site.Client.BaseAddress };
        using var aliased = new HttpRequestMessage(HttpMethod.Post, "mcp") { Content = new StringContent("{}") };
        aliased.Headers.Host = "www.izzyblazor.com";
        Assert.Equal(HttpStatusCode.PermanentRedirect, (await noRedirect.SendAsync(aliased)).StatusCode);

        // Past the limit, a 429 with Retry-After, not the site's HTML. Its own address, so other tests keep theirs.
        HttpResponseMessage? last = null;
        for (var i = 0; i < 301; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "mcp")
            {
                Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", System.Text.Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Accept", "application/json, text/event-stream");
            request.Headers.Add("MCP-Protocol-Version", "2025-06-18");
            request.Headers.Add("Fly-Client-IP", "203.0.113.9");
            last = await site.Client.SendAsync(request);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.NotNull(last.Headers.RetryAfter);
        Assert.DoesNotContain("<html", await last.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Llms_txt_links_every_article_and_component_and_answers_HEAD()
    {
        var text = await site.Client.GetStringAsync("llms.txt");
        Assert.StartsWith("# IzzyBlazor\n", text);
        var links = Link().Matches(text).Select(m => m.Groups[1].Value).ToList();
        Assert.Contains("https://izzyblazor.com/docs/components/button", links);
        Assert.Contains("https://izzyblazor.com/docs/get-started/ai-assistants", links);
        var broken = new List<string>();
        foreach (var path in links.Select(l => l["https://izzyblazor.com/".Length..]).Distinct())
        {
            var response = await site.Client.GetAsync("/" + path);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                broken.Add($"/{path}: {(int)response.StatusCode}");
            }
        }

        Assert.True(broken.Count == 0, string.Join("\n", broken));
        foreach (var file in new[] { "llms.txt", "izzyblazor.api.json" })
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, file);
            Assert.Equal(HttpStatusCode.OK, (await site.Client.SendAsync(head)).StatusCode);
        }
    }

    // No HTML tag, entity or cref left in the Markdown (code blocks and inline code may hold markup).
    private static void Check(List<string> wrong, string what, string markdown)
    {
        var prose = InlineCode().Replace(CodeBlock().Replace(markdown, ""), "");
        if (prose.Contains("cref:", StringComparison.Ordinal))
        {
            wrong.Add($"{what}: cref left");
        }

        if (Tag().Match(prose) is { Success: true } tag)
        {
            wrong.Add($"{what}: HTML left: {prose[Math.Max(0, tag.Index - 60)..Math.Min(prose.Length, tag.Index + 40)]}");
        }

        if (Entity().Match(prose) is { Success: true } entity)
        {
            wrong.Add($"{what}: entity left: {entity.Value}");
        }
    }

    [GeneratedRegex(@"\]\((https://izzyblazor\.com/[^)#]*)")]
    private static partial Regex Link();

    [GeneratedRegex(@"(`{3,}).*?\n\1", RegexOptions.Singleline)]
    private static partial Regex CodeBlock();

    [GeneratedRegex(@"(`+).+?\1")]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"(?<!\\)</?[a-z][\w-]*(\s[^>]*)?/?>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"&(#x?[0-9a-fA-F]+|[a-z]+);")]
    private static partial Regex Entity();
}
