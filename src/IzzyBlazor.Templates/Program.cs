using IzzyBlazor.Generation;

// Exports every starter in the catalogue folder (args[0]) as a dotnet new template folder under args[1].
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: IzzyBlazor.Templates <catalog folder> <output folder>");
    return 1;
}
var count = 0;
if (Directory.Exists(args[0]))
{
    foreach (var directory in Directory.GetDirectories(args[0]).Order(StringComparer.Ordinal))
    {
        if (!File.Exists(Path.Combine(directory, "izzy.json"))) continue;
        var item = ItemDefinition.FromDirectory(directory);
        if (item.Manifest.Kind != ItemKind.Starter) continue;
        TemplateExport.WriteTemplateFolder(item, Path.Combine(args[1], item.Manifest.Id));
        count++;
    }
}
Console.WriteLine($"Exported {count} starter(s) to {args[1]}.");
return 0;
