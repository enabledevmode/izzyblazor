using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using IzzyBlazor.DocsGen;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// Site search: every component, part, API member, article, troubleshooting entry, example and
/// demo, and the sections of the articles and Overviews, built once at startup from the registry,
/// the API metadata, the articles list and the docs pages' sources (embedded as <c>page:</c>
/// resources, read for their headings). <see cref="Search"/> ranks and groups the matches.
/// </summary>
/// <remarks>
/// Headings are read from the Razor source rather than from rendered pages, so startup renders
/// nothing: a heading's text is its literal content, and its id the one the page's outline gives
/// it (<see cref="Heading.SlugOf"/>, numbered for repeats). A site test checks the result against
/// every page's On this page list.
/// </remarks>
public sealed partial class SearchIndex
{
    /// <summary>Queries are cut to this many characters.</summary>
    public const int MaxQuery = 100;

    /// <summary>Headings every Overview or article has. They match only with their page's name.</summary>
    public static readonly IReadOnlySet<string> StandardSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Usage", "Details", "Parts", "Mode support", "Accessibility", "Parameters", "See also",
        "Problem", "Cause", "Fix", "Related", "Next steps",
    };

    private readonly List<SearchEntry> _entries = [];

    public SearchIndex(Registry registry, ApiMetadata api)
    {
        var pages = ReadPages(registry);
        AddComponents(registry, api, pages);
        AddArticles(pages);
        AddApi(registry, api);
    }

    public IReadOnlyList<SearchEntry> Entries => _entries;

    /// <summary>The matches for a query, grouped, with at most <paramref name="perGroup"/> in each group.</summary>
    public SearchResults Search(string? query, int perGroup = 50)
    {
        query = (query ?? "").Trim();
        if (query.Length > MaxQuery)
        {
            query = query[..MaxQuery];
        }

        var terms = Terms(query);
        if (terms.Count == 0)
        {
            return new SearchResults(query, [], 0);
        }

        var squashed = Squash(query);
        var hits = new List<(SearchEntry Entry, int Score)>();
        foreach (var entry in _entries)
        {
            if (Score(entry, terms, squashed) is { } score)
            {
                hits.Add((entry, score));
            }
        }

        // Entries that share a link (a type and its values) show once, the best of them.
        hits = hits.GroupBy(h => h.Entry.Href).Select(g => g.MaxBy(h => h.Score)).ToList();
        var groups = hits
            .GroupBy(h => h.Entry.Group)
            .Select(g => g.OrderByDescending(h => h.Score).ThenBy(h => h.Entry.Order).ToList())
            .OrderByDescending(g => g[0].Score)
            .ThenBy(g => g[0].Entry.Group)
            .Select(g => new SearchResultGroup(g[0].Entry.Group, g.Take(perGroup).Select(h => h.Entry).ToList(), g.Count))
            .ToList();
        return new SearchResults(query, groups, hits.Count) { Marks = string.Join(' ', terms.Concat(terms.Select(Singular)).Distinct()) };
    }

    // "buttons" matches Button (Matches), so it marks "button" too.
    private static string Singular(string term) =>
        term.Length > 3 && term.EndsWith("es", StringComparison.Ordinal) ? term[..^2]
        : term.Length > 3 && term[^1] == 's' ? term[..^1]
        : term;

    // ---- Ranking ----

    // Tiers, best first: the whole query is the title or a keyword; the title starts with it; every
    // word is in the title; in the title or context; anywhere. Then the kind, then the shorter title.
    private static int? Score(SearchEntry entry, IReadOnlyList<string> terms, string squashed)
    {
        int tier;
        if (entry.Standard)
        {
            // These need both their own name and their page's: "accessibility" alone would list every
            // Overview's section, and "button" alone every section of every *Button page.
            if (!terms.All(t => Matches(t, entry.TitleWords) || Matches(t, entry.ContextWords))
                || !terms.Any(t => Matches(t, entry.ContextWords)) || !terms.Any(t => Matches(t, entry.TitleWords)))
            {
                return null;
            }

            tier = 4;
        }
        else if (entry.SquashedTitle == squashed || entry.SquashedKeywords.Contains(squashed))
        {
            tier = 1;
        }
        else if (entry.SquashedTitle.StartsWith(squashed, StringComparison.Ordinal))
        {
            tier = 2;
        }
        else if (terms.All(t => Matches(t, entry.TitleWords)))
        {
            tier = 3;
        }
        else if (terms.All(t => Matches(t, entry.TitleWords) || Matches(t, entry.ContextWords) || Matches(t, entry.KeywordWords)))
        {
            tier = 4;
        }
        else if (terms.All(t => Matches(t, entry.TitleWords) || Matches(t, entry.ContextWords) || Matches(t, entry.KeywordWords) || Matches(t, entry.SummaryWords)))
        {
            tier = 5;
        }
        else
        {
            return null;
        }

        // An enum value named like the query ("Dark") is rarely what's wanted: no better than a word in a title.
        if (entry.Kind == SearchKind.TypeMember)
        {
            tier = Math.Max(tier, 3);
        }

        return (6 - tier) * 10_000 + Weight(entry.Kind) * 100 - Math.Min(entry.Title.Length, 99);
    }

    private static int Weight(SearchKind kind) => kind switch
    {
        SearchKind.Component => 9,
        SearchKind.Article or SearchKind.Troubleshooting => 8,
        SearchKind.Part => 7,
        SearchKind.Section => 6,
        SearchKind.Type or SearchKind.StyleProp => 5,
        SearchKind.Member => 4,
        SearchKind.TypeMember => 3,
        SearchKind.Demo => 2,
        _ => 1,
    };

    // A query word matches a word that starts with it, or that it is plus an "s" or "es" ("buttons").
    private static bool Matches(string term, IReadOnlyList<string> words)
    {
        foreach (var word in words)
        {
            if (word.StartsWith(term, StringComparison.Ordinal)
                || (term.Length > 3 && term[^1] == 's' && (word == term[..^1] || (term[^2] == 'e' && word == term[..^2]))))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The query's words: lowercase runs of letters and digits.</summary>
    internal static List<string> Terms(string text)
    {
        var terms = new List<string>();
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var inWord = i < text.Length && char.IsLetterOrDigit(text[i]);
            if (inWord && start < 0)
            {
                start = i;
            }
            else if (!inWord && start >= 0)
            {
                terms.Add(text[start..i].ToLowerInvariant());
                start = -1;
            }
        }

        return terms;
    }

    /// <summary>
    /// An entry's words: each run of letters and digits, and the words inside a PascalCase name
    /// too, so <c>TextField</c> is "textfield", "text" and "field".
    /// </summary>
    internal static List<string> Words(string? text)
    {
        var words = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return words;
        }

        foreach (Match run in WordRun().Matches(text))
        {
            var value = run.Value;
            words.Add(value.ToLowerInvariant());
            var start = 0;
            for (var i = 1; i < value.Length; i++)
            {
                var hump = char.IsUpper(value[i]) && (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1])
                    || (char.IsUpper(value[i - 1]) && i + 1 < value.Length && char.IsLower(value[i + 1])));
                if (hump)
                {
                    words.Add(value[start..i].ToLowerInvariant());
                    start = i;
                }
            }

            if (start > 0)
            {
                words.Add(value[start..].ToLowerInvariant());
            }
        }

        return words;
    }

    internal static string Squash(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }

    // ---- Building ----

    private void Add(SearchKind kind, string title, string? context, string? summary, string href, IReadOnlyList<string>? keywords = null, bool standard = false) =>
        _entries.Add(new SearchEntry(kind, title, context, OverviewText.Sentence(summary, 180), href)
        {
            Keywords = keywords ?? [],
            Standard = standard,
            Order = _entries.Count,
        });

    private void AddComponents(Registry registry, ApiMetadata api, Dictionary<string, SourcePage> pages)
    {
        foreach (var component in registry.Components)
        {
            var overview = Registry.OverviewHref(component);
            Add(SearchKind.Component, component.Name, component.Category, component.Summary, overview, component.Keywords);

            foreach (var part in component.Parts)
            {
                if (registry.ApiHref(part, null) is { } href)
                {
                    Add(SearchKind.Part, part, $"Part of {component.Name}", ApiMetadata.PlainText(api.Component(part)?.Summary), href);
                }
            }

            if (pages.TryGetValue(overview, out var page))
            {
                AddSections(page, component.Name, SearchGroup.Components);
                foreach (var id in page.Examples)
                {
                    if (registry.Example(id) is { } found && !page.Headings.Any(h => string.Equals(h.Text, found.Example.Title, StringComparison.OrdinalIgnoreCase)))
                    {
                        Add(SearchKind.Example, found.Example.Title, component.Name, Lead(found.Example.Lead), $"{overview}#ex-{id}");
                    }
                }
            }
            else if (api.Component(component.Name) is { } apiComponent)
            {
                // A generated Overview (GeneratedOverview): a section per example, under its title.
                var headings = new List<string>();
                if (apiComponent.Examples is { Count: > 0 })
                {
                    headings.Add("Usage");
                }

                headings.AddRange(component.Examples.Select(e => e.Title));
                if (apiComponent.Remarks is not null)
                {
                    headings.Add("Details");
                }

                headings.Add("Mode support");
                if (apiComponent.Accessibility is not null)
                {
                    headings.Add("Accessibility");
                }

                headings.Add("Parameters");
                var ids = Ids(component.Name, headings.Select(h => (h, (string?)null)));
                for (var i = 0; i < headings.Count; i++)
                {
                    var example = component.Examples.FirstOrDefault(e => e.Title == headings[i]);
                    Add(SearchKind.Section, headings[i], component.Name, Lead(example?.Lead), $"{overview}#{ids[i]}", standard: StandardSections.Contains(headings[i]));
                }
            }

            foreach (var scenario in component.Scenarios)
            {
                Add(SearchKind.Demo, scenario.Title, $"{component.Name} demo", scenario.Description, Registry.ScenarioHref(component, scenario));
            }
        }
    }

    private void AddArticles(Dictionary<string, SourcePage> pages)
    {
        foreach (var section in Articles.Sections)
        {
            var troubleshooting = section.Slug == "troubleshooting";
            var group = troubleshooting ? SearchGroup.Troubleshooting : SearchGroup.Articles;
            foreach (var article in section.Articles)
            {
                var href = section.HrefOf(article);
                var context = troubleshooting && article.Components.Count > 0 ? string.Join(", ", article.Components) : section.Title;
                Add(troubleshooting ? SearchKind.Troubleshooting : SearchKind.Article, article.Title, context, article.Summary, href);
                if (pages.TryGetValue(href, out var page))
                {
                    AddSections(page, article.Title, group);
                }
            }
        }

        // Style props lists the shared bases under headings built from SharedProps.
        if (Articles.Find("common", "style-props") is { } styleProps)
        {
            foreach (var shared in SharedProps.Sections)
            {
                Add(SearchKind.Section, shared.Title, styleProps.Article.Title, shared.Description,
                    $"{styleProps.Section.HrefOf(styleProps.Article)}#{ApiMetadata.Anchor(shared.Base)}");
            }
        }
    }

    private void AddSections(SourcePage page, string context, SearchGroup group)
    {
        foreach (var heading in page.Headings)
        {
            _entries.Add(new SearchEntry(SearchKind.Section, heading.Text, context, heading.Lead, $"{page.Href}#{heading.Id}")
            {
                Standard = StandardSections.Contains(heading.Text),
                SectionGroup = group,
                Order = _entries.Count,
            });
        }
    }

    private void AddApi(Registry registry, ApiMetadata api)
    {
        foreach (var component in registry.Components)
        {
            foreach (var name in component.Parts.Prepend(component.Name))
            {
                if (api.Component(name) is not { } apiComponent)
                {
                    continue;
                }

                AddMembers(registry, name, apiComponent.Properties, "parameter");
                AddMembers(registry, name, apiComponent.Events, "event");
                if (name == component.Name)
                {
                    AddMembers(registry, name, apiComponent.Methods, "method");
                }
            }
        }

        foreach (var shared in SharedProps.Sections)
        {
            foreach (var member in api.Base(shared.Base)?.Properties ?? [])
            {
                if (member.Obsolete is null)
                {
                    Add(SearchKind.StyleProp, member.Name, shared.Title, ApiMetadata.PlainText(member.Summary),
                        $"docs/common/style-props#{ApiMetadata.Anchor(shared.Base)}-{ApiMetadata.Anchor(member.Name)}");
                }
            }
        }

        // A type is on the API page of the first component (or part) that lists it.
        var typePages = new Dictionary<string, (string Href, string Owner)>(StringComparer.Ordinal);
        foreach (var component in registry.Components)
        {
            foreach (var name in component.Parts.Prepend(component.Name))
            {
                foreach (var type in api.Component(name)?.Types ?? [])
                {
                    typePages.TryAdd(type, ($"{Registry.ApiPageHref(component)}#type-{ApiMetadata.Anchor(type)}", component.Name));
                }
            }
        }

        foreach (var type in api.File.Types)
        {
            if (type.Obsolete is not null || !typePages.TryGetValue(type.Name, out var page))
            {
                continue;
            }

            Add(SearchKind.Type, type.Name, $"{page.Owner} · {type.Kind}", ApiMetadata.PlainText(type.Summary), page.Href);
            foreach (var member in type.Members.Where(m => m.Obsolete is null))
            {
                Add(SearchKind.TypeMember, member.Name, type.Name, ApiMetadata.PlainText(member.Summary), page.Href);
            }
        }
    }

    private void AddMembers(Registry registry, string owner, IEnumerable<ApiMember> members, string kind)
    {
        foreach (var member in members)
        {
            if (member.Obsolete is null && registry.ApiHref(owner, member.Name) is { } href)
            {
                Add(SearchKind.Member, member.Name, $"{owner} · {kind}", ApiMetadata.PlainText(member.Summary), href);
            }
        }
    }

    // An example's lead without its code marks.
    private static string? Lead(string? lead) => lead?.Replace("`", "", StringComparison.Ordinal);

    // ---- Reading the docs pages' sources ----

    /// <summary>A docs page read from its source: its address, its sections and the examples it shows.</summary>
    internal sealed record SourcePage(string Href, IReadOnlyList<SourceHeading> Headings, IReadOnlyList<string> Examples);

    internal sealed record SourceHeading(string Text, string Id, string? Lead);

    /// <summary>
    /// The articles (by their <c>@page</c> route) and hand-written Overviews (by their component's
    /// name), keyed by address.
    /// </summary>
    private static Dictionary<string, SourcePage> ReadPages(Registry registry)
    {
        var pages = new Dictionary<string, SourcePage>(StringComparer.Ordinal);
        var assembly = typeof(SearchIndex).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith("page:", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();
            var path = resource["page:".Length..].Replace('\\', '/');

            string? href = null;
            string? title = null;
            if (PageRoute().Match(source) is { Success: true } route && route.Groups["route"].Value.StartsWith("/docs/", StringComparison.Ordinal))
            {
                var segments = route.Groups["route"].Value.Trim('/').Split('/');
                if (segments.Length == 3 && Articles.Find(segments[1], segments[2]) is { } found)
                {
                    href = found.Section.HrefOf(found.Article);
                    title = found.Article.Title;
                }
            }
            else if (path.StartsWith("Overviews/", StringComparison.Ordinal) && path.EndsWith("Overview", StringComparison.Ordinal)
                     && registry.ByName(path["Overviews/".Length..^"Overview".Length]) is { } component)
            {
                href = Registry.OverviewHref(component);
                title = component.Name;
            }

            if (href is not null)
            {
                pages[href] = Read(href, title!, source);
            }
        }

        return pages;
    }

    private static SourcePage Read(string href, string title, string source)
    {
        // Ids are numbered in page order, starting with the page's own h1 (its title).
        var found = new List<(string Text, string? ExplicitId, string? Lead)>();
        foreach (Match match in HeadingTag().Matches(source))
        {
            var attributes = match.Groups["attrs"].Value;
            if (LevelAttr().Match(attributes) is not { Success: true } level || level.Groups[1].Value is not ("2" or "3")
                || attributes.Contains("Outline=\"false\"", StringComparison.Ordinal))
            {
                continue;
            }

            var text = Text(match.Groups["text"].Value);
            var explicitId = IdAttr().Match(attributes) is { Success: true } id ? id.Groups[1].Value : null;
            if (text is null || explicitId?.Contains('@') == true)
            {
                continue;
            }

            string? lead = null;
            if (LeadBlock().Match(source, match.Index + match.Length) is { Success: true } block)
            {
                lead = OverviewText.Sentence(Text(block.Groups["text"].Value), 180);
            }

            found.Add((text, explicitId, lead));
        }

        var ids = Ids(title, found.Select(f => (f.Text, f.ExplicitId)));
        var headings = found.Select((f, i) => new SourceHeading(f.Text, ids[i], f.Lead)).ToList();
        var examples = ExampleTag().Matches(source).Select(m => m.Groups["id"].Value).Distinct().ToList();
        return new SourcePage(href, headings, examples);
    }

    /// <summary>
    /// The ids the outline gives these headings, after a page title (the h1): explicit ids are
    /// kept and taken first, the rest are slugs, numbered "-2", "-3" for repeats.
    /// </summary>
    private static List<string> Ids(string title, IEnumerable<(string Text, string? ExplicitId)> headings)
    {
        var all = headings.Prepend((Text: title, ExplicitId: (string?)null)).ToList();
        var taken = new HashSet<string>(all.Select(h => h.ExplicitId).OfType<string>(), StringComparer.Ordinal);
        var ids = new List<string>();
        foreach (var (text, explicitId) in all)
        {
            if (explicitId is not null)
            {
                ids.Add(explicitId);
                continue;
            }

            var slug = Heading.SlugOf(text);
            var id = slug;
            for (var n = 2; taken.Contains(id); n++)
            {
                id = $"{slug}-{n}";
            }

            taken.Add(id);
            ids.Add(id);
        }

        return ids.Skip(1).ToList();
    }

    // Markup to plain text: tags dropped, entities decoded, "@@" as "@", spaces collapsed. Null when
    // it holds a Razor expression, whose text can't be known here.
    private static string? Text(string markup)
    {
        var text = Tags().Replace(markup, "").Replace("@@", "\u0001", StringComparison.Ordinal);
        if (text.Contains('@'))
        {
            return null;
        }

        text = WebUtility.HtmlDecode(text.Replace('\u0001', '@'));
        return Spaces().Replace(text, " ").Trim() is { Length: > 0 } result ? result : null;
    }

    [GeneratedRegex(@"<Heading\b(?<attrs>[^>]*)>(?<text>.*?)</Heading>", RegexOptions.Singleline)]
    private static partial Regex HeadingTag();

    [GeneratedRegex(@"\bLevel=""(\d)""")]
    private static partial Regex LevelAttr();

    [GeneratedRegex(@"\bid=""([^""]*)""")]
    private static partial Regex IdAttr();

    [GeneratedRegex(@"\G\s*<TextBlock\b[^>]*>(?<text>.*?)</TextBlock>", RegexOptions.Singleline)]
    private static partial Regex LeadBlock();

    [GeneratedRegex(@"<ExampleViewer\b[^>]*\bExample=""(?<id>[^""@]+)""")]
    private static partial Regex ExampleTag();

    [GeneratedRegex(@"^@page\s+""(?<route>[^""]+)""", RegexOptions.Multiline)]
    private static partial Regex PageRoute();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRun();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

/// <summary>What a search entry is.</summary>
public enum SearchKind { Component, Part, Section, Article, Troubleshooting, Member, StyleProp, Type, TypeMember, Example, Demo }

/// <summary>The groups results are shown in, in their order when two groups' best results tie.</summary>
public enum SearchGroup { Components, Api, Articles, Troubleshooting, Examples }

/// <summary>One thing search can find: a title, where it is (context), a line about it and its link.</summary>
public sealed record SearchEntry(SearchKind Kind, string Title, string? Context, string? Summary, string Href)
{
    /// <summary>Other names for it, matched like its title (a component's keywords).</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>A heading every page of its kind has ("Accessibility"): it matches only with its page's name.</summary>
    public bool Standard { get; init; }

    /// <summary>Its place in the site's order, for ties.</summary>
    public int Order { get; init; }

    internal SearchGroup? SectionGroup { get; init; }

    public SearchGroup Group => SectionGroup ?? Kind switch
    {
        SearchKind.Component or SearchKind.Part or SearchKind.Section => SearchGroup.Components,
        SearchKind.Member or SearchKind.StyleProp or SearchKind.Type or SearchKind.TypeMember => SearchGroup.Api,
        SearchKind.Article => SearchGroup.Articles,
        SearchKind.Troubleshooting => SearchGroup.Troubleshooting,
        _ => SearchGroup.Examples,
    };

    internal string SquashedTitle => _squashedTitle ??= SearchIndex.Squash(Title);

    internal IReadOnlyList<string> SquashedKeywords => _squashedKeywords ??= Keywords.Select(SearchIndex.Squash).ToList();

    internal IReadOnlyList<string> TitleWords => _titleWords ??= SearchIndex.Words(Title);

    internal IReadOnlyList<string> ContextWords => _contextWords ??= SearchIndex.Words(Context);

    internal IReadOnlyList<string> KeywordWords => _keywordWords ??= Keywords.SelectMany(SearchIndex.Words).ToList();

    internal IReadOnlyList<string> SummaryWords => _summaryWords ??= SearchIndex.Words(Summary);

    private string? _squashedTitle;
    private IReadOnlyList<string>? _squashedKeywords;
    private IReadOnlyList<string>? _titleWords;
    private IReadOnlyList<string>? _contextWords;
    private IReadOnlyList<string>? _keywordWords;
    private IReadOnlyList<string>? _summaryWords;
}

/// <summary>A search's results: its query (trimmed and cut), the groups in order and the total count.</summary>
public sealed record SearchResults(string Query, IReadOnlyList<SearchResultGroup> Groups, int Count)
{
    /// <summary>The words to mark in the results: the query's, and their singulars ("button" for "buttons").</summary>
    public string Marks { get; init; } = Query;
}

/// <summary>A group's results, best first, and how many matched in all.</summary>
public sealed record SearchResultGroup(SearchGroup Group, IReadOnlyList<SearchEntry> Entries, int Total)
{
    public string Title => Group switch
    {
        SearchGroup.Components => "Components",
        SearchGroup.Api => "API",
        SearchGroup.Articles => "Articles",
        SearchGroup.Troubleshooting => "Troubleshooting",
        _ => "Examples",
    };
}
