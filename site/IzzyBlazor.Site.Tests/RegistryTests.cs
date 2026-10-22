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
        var listed = Registry.Components.SelectMany(c => c.Parts.Prepend(c.Name)).ToHashSet();
        var missing = Api.File.Components.Select(c => c.Name).Where(n => !listed.Contains(n)).ToList();
        var unknown = listed.Where(n => Api.Component(n) is null).ToList();

        Assert.True(missing.Count == 0, $"Add to components.json: {string.Join(", ", missing)}");
        Assert.True(unknown.Count == 0, $"Not public components: {string.Join(", ", unknown)}");
    }

    [Fact]
    public void A_feature_row_that_names_a_member_finds_its_modes()
    {
        var broken = Registry.Components
            .SelectMany(c => c.Features.Where(f => f.Member is not null).Select(f => (c.Name, f.Member!)))
            .Where(x => Api.Component(x.Name) is not { } api || IzzyBlazor.Site.Components.Parts.ModeSupportTable.ModesOf(api, x.Item2) is null)
            .Select(x => $"{x.Name}.{x.Item2}")
            .ToList();

        Assert.True(broken.Count == 0, $"No [ModeSupport] on: {string.Join(", ", broken)}");
    }

    [Fact]
    public void Every_example_has_a_file_and_every_file_is_registered()
    {
        var registered = Registry.AllExamples.Select(e => e.Example.Id).ToHashSet();
        var files = Catalog.Ids.ToHashSet();

        Assert.Empty(registered.Except(files));
        Assert.Empty(files.Except(registered));
        Assert.All(files, id => Assert.NotNull(Catalog.SourceOf(id)));
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
                problems.AddRange(article.Modes.Where(m => m is not ("off" or "static" or "live")).Select(m => $"{at}: mode {m}"));
            }
        }

        Assert.True(problems.Count == 0, string.Join(", ", problems));
    }

    [Fact]
    public void Every_component_has_a_picture()
    {
        Assert.Empty(Registry.Components.Select(c => c.Id).Except(Pictures.ById.Keys));
        Assert.Empty(Pictures.ById.Keys.Except(Registry.Components.Select(c => c.Id)));
    }

    [Fact]
    public void Shown_source_leaves_out_hidden_lines()
    {
        var source = ExampleCatalog.Clean("<A />\r\n@* hide *@\n<Scaffolding />\n@* /hide *@\n<B />\n");

        Assert.Equal("<A />\n<B />", source);
    }

    [Fact]
    public void No_example_names_a_control_after_a_form_property()
    {
        // A control named "action" hides the form's own action, and Blazor's enhanced form
        // handling then posts to "[object RadioNodeList]".
        var clashes = Catalog.Ids
            .Where(id => System.Text.RegularExpressions.Regex.IsMatch(Catalog.SourceOf(id)!.Value.Source,
                @"\bName=""(action|method|target|submit|enctype|elements)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .ToList();

        Assert.True(clashes.Count == 0, $"Rename the control in: {string.Join(", ", clashes)}");
    }
}
