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
                    var found = (dot < 0 && (component.Parts.Contains(name) || Api.Type(name) is not null))
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
}
