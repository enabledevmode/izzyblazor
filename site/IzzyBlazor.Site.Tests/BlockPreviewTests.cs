using IzzyBlazor.Site.Services;

namespace IzzyBlazor.Site.Tests;

// The Blocks pages preview each block from components generated from its template
// (Blocks/Previews), so a preview is always what izzy add writes. This keeps them current.
// To regenerate after changing a block: set IZZY_UPDATE_PREVIEWS=1 and run this test.
public class BlockPreviewTests
{
    [Fact]
    public void Each_blocks_previews_are_generated_from_its_template()
    {
        var site = SiteFolder();
        var catalog = new BlockCatalog(Path.Combine(site, "..", "..", "catalog"));
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

    private static string SiteFolder([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "IzzyBlazor.Site"));
}
