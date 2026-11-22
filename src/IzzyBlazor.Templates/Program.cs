using IzzyBlazor.Generation;

// Exports every starter and kit in the catalog folder (args[0]) as a dotnet new template folder under args[1].
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: IzzyBlazor.Templates <catalog folder> <output folder>");
    return 1;
}
var count = 0;
if (Directory.Exists(args[0]))
{
    // Through the catalog, so a kit is exported over its base.
    foreach (var item in Catalog.Load(args[0]).Items.Values)
    {
        // Only what needs no account ships through dotnet new; the rest comes from izzy new.
        if (!item.Manifest.Kind.MakesProject() || item.Manifest.Tier != ItemTier.Free) continue;
        TemplateExport.WriteTemplateFolder(item, Path.Combine(args[1], item.Manifest.Id));
        count++;
    }
}
Console.WriteLine($"Exported {count} starter(s) and kit(s) to {args[1]}.");
return 0;
