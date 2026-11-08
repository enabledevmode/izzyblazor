using IzzyBlazor.Site.Services;

namespace IzzyBlazor.Site.Tests;

// components.json against the library and the Examples folder.
public class RegistryTests
{
    private static readonly ApiMetadata Api = ApiMetadata.Load();
    private static readonly Registry Registry = Registry.Load(Api);
    private static readonly ExampleCatalog Catalog = new();

    [Fact]
    public void Every_public_component_is_documented_as_a_component_or_a_part()
    {
        var listed = Registry.AllComponents.SelectMany(c => c.Parts.Prepend(c.Name)).ToHashSet();
        var missing = Api.File.Components.Select(c => c.Name).Where(n => !listed.Contains(n)).ToList();
        var unknown = listed.Where(n => Api.Component(n) is null).ToList();

        Assert.True(missing.Count == 0, $"Add to components.json: {string.Join(", ", missing)}");
        Assert.True(unknown.Count == 0, $"Not public components: {string.Join(", ", unknown)}");
    }

    // A Usage snippet is read at a glance, with no sideways scrolling on a laptop: the code column
    // holds about 105 characters at a 1280px window. A tag that won't fit puts one attribute on
    // each line. Both the doc comments' snippets and the hand-written Overviews' Usage are checked.
    [Fact]
    public void Usage_snippets_fit_without_scrolling()
    {
        var snippets = Api.File.Components.SelectMany(c => (c.Examples ?? []).Select(e => (c.Name, Code: e)))
            .Concat(HandWrittenUsage());
        var wide = snippets
            .SelectMany(s => s.Code.Split('\n').Where(l => l.TrimEnd().Length > 100).Select(l => $"{s.Name}: {l.Trim()}"))
            .ToList();

        Assert.True(wide.Count == 0, $"Usage lines over 100 characters:\n{string.Join("\n", wide)}");

        // A child element starts its own line: no "<FormActions><Button>" or "<A /><B />" on one line
        // (text with an element in it, "Read the <Anchor>docs</Anchor>.", is fine).
        var nested = snippets
            .SelectMany(s => s.Code.Split('\n')
                .Where(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"(<[A-Za-z][^<>]*[^/<>]|/|</[\w.]+)>\s*<[A-Za-z]") &&
                            System.Text.RegularExpressions.Regex.Replace(l, "<[^>]*>", "").Trim().Length == 0)
                .Select(l => $"{s.Name}: {l.Trim()}"))
            .ToList();
        Assert.True(nested.Count == 0, $"Usage lines with a child element on the same line:\n{string.Join("\n", nested)}");
    }

    // Every component page opens with a Usage: a series presented as its own chart (Bar chart is
    // ChartBarSeries) shows the whole chart, as any other component does.
    [Fact]
    public void Every_component_page_has_a_usage()
    {
        var handWritten = HandWrittenUsage().Select(u => u.Name[..^"Overview".Length]).ToHashSet();
        var missing = Registry.AllComponents
            .Where(c => Api.Component(c.Name)?.Examples is not { Count: > 0 } && !handWritten.Contains(c.Name))
            .Select(c => c.Name)
            .ToList();

        Assert.True(missing.Count == 0, $"Add an <example><code> to: {string.Join(", ", missing)}");
    }

    private static IEnumerable<string> Ancestors(string start)
    {
        for (var dir = new DirectoryInfo(Path.GetFullPath(start)); dir is not null; dir = dir.Parent)
        {
            yield return dir.FullName;
        }
    }

    // Each hand-written Overview's `const string Usage = """…"""`, dedented as C# does.
    private static IEnumerable<(string Name, string Code)> HandWrittenUsage([System.Runtime.CompilerServices.CallerFilePath] string here = "")
    {
        // The source path where the build keeps it; CI's deterministic build maps it to "/_/", so
        // then the repository is found above the test's output or the working directory.
        var inRepo = Path.Combine("site", "IzzyBlazor.Site", "Docs", "Overviews");
        var folder = new[] { Path.Combine(Path.GetDirectoryName(here)!, "..", "..") , AppContext.BaseDirectory, Environment.CurrentDirectory }
            .SelectMany(start => Ancestors(start))
            .Select(dir => Path.Combine(dir, inRepo))
            .FirstOrDefault(Directory.Exists) ?? throw new DirectoryNotFoundException($"No {inRepo} above the tests.");
        foreach (var file in Directory.GetFiles(folder, "*.razor"))
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), "const string Usage = \"\"\"\\r?\\n(.*?)\\r?\\n([ \\t]*)\"\"\"", System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                var indent = m.Groups[2].Value.Length;
                var code = string.Join('\n', m.Groups[1].Value.Split('\n').Select(l => l.Length > indent ? l[indent..] : l.TrimStart()));
                yield return (Path.GetFileNameWithoutExtension(file), code);
            }
        }
    }

    [Fact]
    public void Every_example_has_a_file_and_every_file_is_registered()
    {
        var registered = Registry.AllExamples.Select(e => e.Example.Id).ToHashSet();
        var files = Catalog.Ids.ToHashSet();

        Assert.Empty(registered.Except(files));
        Assert.Empty(files.Except(registered));
        Assert.All(files, id => Assert.NotNull(Catalog.SourceOf(id)));

        // A model on a tab of its own, and the line that names it left out of what's shown.
        Assert.Equal("EditableOrder.cs", Assert.Single(Catalog.FilesOf("data-grid-editing")).FileName);
        Assert.DoesNotContain("file:", Catalog.SourceOf("data-grid-editing")!.Value.Source);
    }

    [Fact]
    public void Every_member_a_topic_lists_is_in_the_API()
    {
        // A topic's API page shows only what it lists, so a renamed member would quietly drop off it.
        var unknown = new List<string>();
        foreach (var component in Registry.AllComponents)
        {
            var api = Api.Component(component.Name)!;
            var column = Api.Component("PropertyColumn")!;
            foreach (var topic in component.Topics)
            {
                foreach (var name in topic.Members)
                {
                    var dot = name.IndexOf('.');
                    var owner = dot < 0 ? api : name[..dot] == "Column" ? column : Api.Component(name[..dot]);
                    var member = dot < 0 ? name : name[(dot + 1)..];
                    var found = (dot < 0 && (component.Parts.Contains(name) || Api.Type(name) is not null || Api.Component(name) is not null))
                        || (owner is not null && owner.Properties.Concat(owner.Events).Concat(owner.Methods).Any(m => m.Name == member));
                    if (!found)
                    {
                        unknown.Add($"{component.Id}/{topic.Id}: {name}");
                    }
                }
            }
        }

        Assert.True(unknown.Count == 0, $"Not in the API: {string.Join(", ", unknown)}");
    }

    [Fact]
    public void Every_pictogram_is_a_Lucide_icon()
    {
        var unknown = Registry.Components.Where(c => IzzyBlazor.Site.Components.Parts.Pictogram.Lookup(c.Icon) is null).Select(c => $"{c.Id}: {c.Icon}").ToList();

        Assert.True(unknown.Count == 0, string.Join(", ", unknown));
    }

    [Fact]
    public void Every_article_has_a_Lucide_icon_and_tags_known_components_and_modes()
    {
        var problems = new List<string>();
        foreach (var section in Articles.Sections)
        {
            if (IzzyBlazor.Site.Components.Parts.Pictogram.Lookup(section.Icon) is null)
            {
                problems.Add($"{section.Slug}: icon {section.Icon}");
            }

            foreach (var article in section.Articles)
            {
                var at = $"{section.Slug}/{article.Slug}";
                problems.AddRange(new[] { article.Icon }.Where(i => IzzyBlazor.Site.Components.Parts.Pictogram.Lookup(i) is null).Select(i => $"{at}: icon {i}"));
                problems.AddRange(article.Components.Where(c => Registry.ByName(c) is null).Select(c => $"{at}: component {c}"));
                problems.AddRange(article.Modes.Where(m => m is not ("noscript" or "static" or "interactive")).Select(m => $"{at}: mode {m}"));
            }
        }

        Assert.True(problems.Count == 0, string.Join(", ", problems));
    }

    [Fact]
    public void Every_component_has_a_picture()
    {
        Assert.Empty(Registry.AllComponents.Select(c => c.Id).Except(Pictures.ById.Keys));
        Assert.Empty(Pictures.ById.Keys.Except(Registry.AllComponents.Select(c => c.Id)));
    }

    [Fact]
    public void Example_leads_and_topic_intros_stay_short()
    {
        // CONTRIBUTING's writing style: a sentence or two before an example, details after it.
        var long_ = new List<string>();
        void Check(string where, string text, int words, int sentences)
        {
            var (w, s) = (Words(text), Sentences(text));
            if (w > words || s > sentences)
            {
                long_.Add($"{where} ({w} words, {s} sentences)");
            }
        }

        foreach (var component in Registry.AllComponents)
        {
            foreach (var topic in component.Topics)
            {
                topic.Intro.ForEach(p => Check($"{component.Id}/{topic.Id} intro", p, 80, 4));
            }

            foreach (var example in component.Examples.Concat(component.Topics.SelectMany(t => t.Examples)))
            {
                if (example.Lead is { } lead)
                {
                    Check($"{component.Id}: {example.Id} lead", lead, 60, 3);
                }

                example.After.ForEach(p => Check($"{component.Id}: {example.Id} after", p, 60, 3));
            }
        }

        Assert.True(long_.Count == 0, $"Too long (a lead or after paragraph: 60 words, 3 sentences; an intro: 80 words, 4 sentences); move details under the example:\n{string.Join("\n", long_)}");
    }

    private static int Words(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    // Sentences, not counting the dots inside `code` or in "e.g.".
    private static int Sentences(string text)
    {
        var plain = System.Text.RegularExpressions.Regex.Replace(text, "`[^`]*`", "x");
        plain = System.Text.RegularExpressions.Regex.Replace(plain, @"\b(e\.g|i\.e|etc|vs)\.", "x");
        return System.Text.RegularExpressions.Regex.Split(plain.Trim(), @"(?<=[.!?])\s+(?=[A-Z(""x])").Length;
    }
}
