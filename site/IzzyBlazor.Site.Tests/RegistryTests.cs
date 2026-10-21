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
}
