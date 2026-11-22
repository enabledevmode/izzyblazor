using IzzyBlazor.Site.Services;

namespace IzzyBlazor.Site.Tests;

// The Blocks pages preview each block from components generated from its template
// (Blocks/Previews), so a preview is always what izzy add writes. This keeps them current.
// To regenerate after changing a block: set IZZY_UPDATE_PREVIEWS=1 and run this test.
// The templates are in the private catalog (IZZY_PRO_CATALOG, or izzyblazor-pro-catalog beside the
// repo); without it the previews in the repo are taken as they are and these tests skip.
public class BlockPreviewTests
{
    private static string? PrivateCatalog()
    {
        var folder = Environment.GetEnvironmentVariable("IZZY_PRO_CATALOG") is { Length: > 0 } given
            ? given : Path.Combine(RegistryTests.SiteFolder(), "..", "..", "..", "izzyblazor-pro-catalog");
        return Directory.Exists(Path.Combine(folder, "hero")) ? Path.GetFullPath(folder) : null;
    }

    private sealed class TemplateFactAttribute : FactAttribute
    {
        public TemplateFactAttribute()
        {
            if (PrivateCatalog() is null) Skip = "Needs the private catalog (IZZY_PRO_CATALOG, or izzyblazor-pro-catalog beside the repo).";
        }
    }

    private static BlockCatalog Catalog() =>
        new(Path.Combine(RegistryTests.SiteFolder(), "..", "..", "catalog"), PrivateCatalog());

    // A block's page says which files izzy add writes from its manifest, as the site has no template.
    [TemplateFact]
    public void Each_blocks_manifest_lists_the_files_it_writes()
    {
        var wrong = Catalog().All.Where(b => !b.Manifest.Writes.SequenceEqual(BlockCatalog.GeneratedWrites(b)))
            .Select(b => $"{b.Id}: \"writes\": [{string.Join(", ", BlockCatalog.GeneratedWrites(b).Select(w => $"\"{w}\""))}]").ToList();
        Assert.True(wrong.Count == 0, $"Set these in catalog/<id>/izzy.json: {string.Join("; ", wrong)}");
    }

    [TemplateFact]
    public void Each_blocks_previews_are_generated_from_its_template()
    {
        var site = RegistryTests.SiteFolder();
        var catalog = Catalog();
        Assert.NotEmpty(catalog.All);
        var update = Environment.GetEnvironmentVariable("IZZY_UPDATE_PREVIEWS") == "1";

        var expected = catalog.All.SelectMany(BlockCatalog.PreviewFiles).ToList();
        var stale = new List<string>();
        foreach (var (path, content) in expected)
        {
            var file = Path.Combine(site, path);
            if (File.Exists(file) && File.ReadAllBytes(file).AsSpan().SequenceEqual(content))
            {
                continue;
            }
            stale.Add(path);
            if (update)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllBytes(file, content);
            }
        }

        // A preview no variant generates any more (a variation or a block removed).
        var previews = Path.Combine(site, BlockCatalog.PreviewFolder);
        var wanted = expected.Select(e => Path.GetFullPath(Path.Combine(site, e.Path))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.Exists(previews) ? Directory.EnumerateFiles(previews, "*.razor", SearchOption.AllDirectories) : [])
        {
            if (wanted.Contains(Path.GetFullPath(file)))
            {
                continue;
            }
            stale.Add(Path.GetRelativePath(site, file));
            if (update)
            {
                File.Delete(file);
            }
        }

        Assert.True(update || stale.Count == 0,
            $"Block previews are out of date; set IZZY_UPDATE_PREVIEWS=1 and run this test: {string.Join(", ", stale)}");
    }
}
