using System.Text.Json.Nodes;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// izzy add component: the docs' usage example of every component, put on a page that already has a form,
/// directives and a @code block of its own, in an app that then builds. The examples are the docs', so this
/// is what keeps a new one from being written in a way that can't be moved to another page.
/// </summary>
[Collection(ProjectBuild.Collection)]
public sealed class ComponentTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-components-").FullName;

    (int Exit, string Out, string Error) Izzy(params string[] args) => Izzy(ItemTier.Pro, args);

    // With no account service the run has the access it's given: an account's, or Pro's.
    (int Exit, string Out, string Error) Izzy(ItemTier access, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _work, output, error, access: access).Run(args);
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task Every_usage_example_goes_onto_a_page_with_a_form_and_the_app_builds()
    {
        ChangesetWriter.Apply(Generator.Generate(ProjectBuild.Item("blazor"), ProjectBuild.StarterSettings(), ProjectInfo.ForNewProject("Acme")), _work);
        var pages = Path.Combine(_work, "Components", "Pages");
        var contact = File.ReadAllLines(Path.Combine(pages, "Contact.razor"));
        var line = Array.FindIndex(contact, l => l.StartsWith("<Container", StringComparison.Ordinal)) + 1;

        var added = new List<string>();
        foreach (var name in JsonNode.Parse(Izzy("list", "component", "--json").Out)!.AsArray().Select(c => (string)c!["name"]!))
        {
            // A copy of the Contact page for each: its form has a Model, a Send and an OnInitialized already.
            var page = $"Components/Pages/With{name}.razor";
            File.WriteAllLines(Path.Combine(_work, page), contact.Select((l, i) => i == 0 ? $"@page \"/with-{name}\"" : l));
            var result = Izzy("add", "component", name, "--at", $"{page}:{line}");
            if (result.Exit == 0)
            {
                added.Add(name);
                continue;
            }
            Assert.Contains("has no usage example", result.Error);
            File.Delete(Path.Combine(_work, page));
        }
        Assert.True(added.Count >= 160, $"Only {added.Count} components have a usage example to add.");

        // Button's example is a form too: its Model takes a number, and its form another name.
        var button = File.ReadAllText(Path.Combine(pages, "WithButton.razor"));
        Assert.Contains("<Form Model=\"Model2\" FormName=\"contact-email\"", button);
        Assert.Contains("<Button Submit>Save</Button>", button);
        Assert.Contains("private void OnInitialized2() => Model2 ??= new();", button);

        // A new page and a new component are in the build too: izzy add page and izzy add razor, signed out.
        var page2 = Izzy(ItemTier.Free, "add", "page", "--name", "MonthlyReports", "--nav");
        Assert.True(page2.Exit == 0, page2.Error);
        var reports = File.ReadAllText(Path.Combine(pages, "MonthlyReports.razor"));
        Assert.StartsWith("@page \"/monthly-reports\"\n\n<PageTitle>Monthly reports</PageTitle>", reports.ReplaceLineEndings("\n"));
        Assert.Contains("<NavItem Href=\"monthly-reports\"", File.ReadAllText(Path.Combine(_work, "Components", "Layout", "MainLayout.razor")));
        // The file, and the route under another name, are refused with the file named.
        Assert.Contains("Components/Pages/MonthlyReports.razor is already there", Izzy(ItemTier.Free, "add", "page", "--name", "MonthlyReports").Error);
        Assert.Contains("/monthly-reports is already the route of Components/Pages/MonthlyReports.razor", Izzy(ItemTier.Free, "add", "page", "--name", "Reports", "--route", "/monthly-reports").Error);
        // The @rendermode's own name is read as the mode (the VS Code extension sends it).
        var live = Izzy(ItemTier.Free, "add", "page", "--name", "LiveOrders", "--mode", "InteractiveServer");
        Assert.True(live.Exit == 0, live.Error);
        Assert.Contains("@rendermode InteractiveServer", File.ReadAllText(Path.Combine(pages, "LiveOrders.razor")));
        // Markup moves and the app still builds: a wrapper put on and taken off leaves the page as it was, and
        // part of a page becomes a component that takes what it read from the page's @code as a parameter.
        const string weather = "Components/Pages/Weather.razor", counter = "Components/Pages/Counter.razor";
        string At(string page, string tag) => $"{page}:{Array.FindIndex(File.ReadAllLines(Path.Combine(_work, page)), l => l.TrimStart().StartsWith(tag, StringComparison.Ordinal)) + 1}:{File.ReadAllLines(Path.Combine(_work, page)).First(l => l.TrimStart().StartsWith(tag, StringComparison.Ordinal)).IndexOf('<') + 2}";
        var before = File.ReadAllText(Path.Combine(_work, weather));
        Assert.Equal(3, Izzy(ItemTier.Account, "wrap", At(weather, "<PageHeader"), "--with", "Stack").Exit);
        var wrapped = Izzy("wrap", At(weather, "<PageHeader"), "--with", "stack", "--gap", "2");
        Assert.True(wrapped.Exit == 0, wrapped.Error);
        Assert.Contains("        <Stack Gap=\"2\">\n            <PageHeader Title=\"Weather\"", File.ReadAllText(Path.Combine(_work, weather)).ReplaceLineEndings("\n"));
        Assert.Contains("\"component\": \"Row\"", Izzy("wrap", $"{At(weather, "<PropertyColumn")}-{At(weather, "<PropertyColumn Property=\"f => f.Summary\"")[(weather.Length + 1)..]}", "--suggest", "--json").Out);
        Assert.Equal(0, Izzy("unwrap", At(weather, "<Stack")).Exit);
        Assert.Equal(before, File.ReadAllText(Path.Combine(_work, weather)));
        Assert.Contains("<DataGrid> holds <Columns>", Izzy("unwrap", At(weather, "<DataGrid")).Error);
        var extracted = Izzy("extract", At(counter, "<PageHeader"), "--name", "CounterHeader", "--folder", "Components/Shared");
        Assert.True(extracted.Exit == 0 && extracted.Error.Length == 0, extracted.Error);
        Assert.Contains("<CounterHeader Mode=\"@Mode\" />", File.ReadAllText(Path.Combine(_work, counter)));
        Assert.Contains("[Parameter] public (string Text, Status Status, string Note) Mode { get; set; }", File.ReadAllText(Path.Combine(_work, "Components", "Shared", "CounterHeader.razor")));
        // What can't be passed as a value is said, not guessed.
        Assert.Contains("IncrementCount() is a method of", Izzy("extract", At(counter, "<Row"), "--name", "CounterActions", "--dry-run").Error);
        Assert.Contains("\"id\": \"sdk\"", Izzy(ItemTier.Free, "doctor", "--json").Out);

        // The three starter pages are in the build too, each from a free account; signed out, izzy says to sign in.
        // Their files are in the private catalog: where it isn't on this machine, they are left out.
        Assert.Equal(4, Izzy(ItemTier.Free, "add", "page", "--name", "Projects", "--from", "list").Exit);
        if (Directory.Exists(ProjectBuild.PrivateCatalog))
        {
            foreach (var (name, from) in new[] { ("Projects", "list"), ("ProjectDetails", "detail"), ("NewProject", "form") })
            {
                var error = new StringWriter();
                var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _work, new StringWriter(), error, proRoot: ProjectBuild.PrivateCatalog, access: ItemTier.Account)
                    .Run(["add", "page", "--name", name, "--from", from]);
                Assert.True(exit == 0, error.ToString());
            }
            Assert.Contains("FormName=\"new-project\"", File.ReadAllText(Path.Combine(pages, "NewProject.razor")));
        }

        // A component in a new folder brings its @using, once.
        Assert.Equal(0, Izzy(ItemTier.Free, "add", "razor", "--name", "ReportSummary", "--folder", "Components/Shared").Exit);
        Assert.Equal(0, Izzy(ItemTier.Free, "add", "razor", "--name", "ReportTotals", "--folder", "Components/Shared").Exit);
        Assert.Single(File.ReadAllLines(Path.Combine(_work, "Components", "_Imports.razor")), l => l.Trim() == "@using Acme.Components.Shared");
        // A folder's name is written as Razor writes it in a namespace.
        Assert.Equal(0, Izzy(ItemTier.Free, "add", "razor", "--name", "AreaNote", "--folder", "Components/my-area").Exit);
        Assert.Contains("@using Acme.Components.my_area", File.ReadAllText(Path.Combine(_work, "Components", "_Imports.razor")));
        File.AppendAllText(Path.Combine(pages, "MonthlyReports.razor"), "<ReportSummary />\n");

        ProjectBuild.PointAtRepo(_work);
        await ProjectBuild.BuildAsync(_work, "Acme with every usage example");
    }

    // An example's sample-data file goes in the namespace of the project the page is in: the .Client one's here.
    [Fact]
    public void An_example_on_a_Client_page_puts_its_file_in_the_Client_namespace()
    {
        ChangesetWriter.Apply(Generator.Generate(ProjectBuild.Item("blazor"), ProjectBuild.StarterSettings("Interactivity=Auto"), ProjectInfo.ForNewProject("Acme")), _work);
        var result = Izzy("add", "component", "Table", "--at", "Acme.Client/Pages/Counter.razor:3");
        Assert.True(result.Exit == 0, result.Error);
        Assert.Contains("namespace Acme.Client.Pages;", File.ReadAllText(Path.Combine(_work, "Acme.Client", "Pages", "Orders.cs")));
    }

    // The page's names stay the page's: a type that one of its members hides is reached by a @using name, and a
    // second copy of an example gets a local of its own.
    [Fact]
    public void An_example_keeps_clear_of_the_names_a_page_has()
    {
        ChangesetWriter.Apply(Generator.Generate(ProjectBuild.Item("blazor"), ProjectBuild.StarterSettings(), ProjectInfo.ForNewProject("Acme")), _work);
        int Line(string page, string start) => Array.FindIndex(File.ReadAllLines(Path.Combine(_work, page)), l => l.StartsWith(start, StringComparison.Ordinal)) + 1;

        // Home has an Orders list of its own, and Table's example an Orders class.
        const string home = "Components/Pages/Home.razor";
        var result = Izzy("add", "component", "Table", "--at", $"{home}:{Line(home, "<Column")}");
        Assert.True(result.Exit == 0 && result.Error.Length == 0, result.Error);
        var text = File.ReadAllText(Path.Combine(_work, home));
        Assert.Contains("@using Orders2 = Acme.Components.Pages.Orders", text);
        Assert.Contains("<Table Items=\"Orders2.All\"", text);
        Assert.Contains("<Chart Items=\"Orders\"", text);

        const string weather = "Components/Pages/Weather.razor";
        for (var copy = 0; copy < 2; copy++)
            Assert.Equal(0, Izzy("add", "component", "Diff", "--example", "computed", "--at", $"{weather}:{Line(weather, "<Container")}").Exit);
        text = File.ReadAllText(Path.Combine(_work, weather));
        Assert.Contains("var lines = TextDiff.Compare(Before, After);", text);
        Assert.Contains("var lines2 = TextDiff.Compare(Before2, After2);", text);
        Assert.Contains("<Diff Lines=\"lines2\" />", text);
    }

    // An app restored against a package that holds the data is described by that data: its own version's.
    [Fact]
    public void An_app_is_described_by_the_data_in_its_own_package()
    {
        ChangesetWriter.Apply(Generator.Generate(ProjectBuild.Item("blazor"), ProjectBuild.StarterSettings(), ProjectInfo.ForNewProject("Acme")), _work);
        Assert.Equal("tool", (string)JsonNode.Parse(Izzy("describe", "component", "--all", "--json").Out)!["source"]!);

        // What a restore leaves: obj/project.assets.json naming the package's folder, and the package with its data.
        var packages = Path.Combine(_work, "packages");
        var data = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "components.json")))!;
        data["version"] = "9.9.9";
        Directory.CreateDirectory(Path.Combine(packages, "izzyblazor.core", "9.9.9", "tools", "izzy"));
        File.WriteAllText(Path.Combine(packages, "izzyblazor.core", "9.9.9", "tools", "izzy", "components.json"), data.ToJsonString());
        Directory.CreateDirectory(Path.Combine(_work, "obj"));
        File.WriteAllText(Path.Combine(_work, "obj", "project.assets.json"), new JsonObject
        {
            ["libraries"] = new JsonObject { ["IzzyBlazor.Core/9.9.9"] = new JsonObject { ["type"] = "package", ["path"] = "izzyblazor.core/9.9.9" } },
            ["packageFolders"] = new JsonObject { [packages + Path.DirectorySeparatorChar] = new JsonObject() },
        }.ToJsonString());

        var described = JsonNode.Parse(Izzy("describe", "component", "--all", "--json").Out)!;
        Assert.Equal("package", (string)described["source"]!);
        Assert.Equal("9.9.9", (string)described["version"]!);
    }

    [Fact]
    public void A_tag_is_inspected_with_an_account_and_its_options_set_with_Pro()
    {
        ChangesetWriter.Apply(Generator.Generate(ProjectBuild.Item("blazor"), ProjectBuild.StarterSettings(), ProjectInfo.ForNewProject("Acme")), _work);
        const string page = "Components/Pages/Orders.razor";
        // A tag over two lines, with a value that's an expression and one that's text.
        File.WriteAllText(Path.Combine(_work, page), "@page \"/orders\"\n\n<Card>\n    <Button Variant=\"ButtonVariant.Outline\" Size=\"@_size\" Submit\n            Href=\"a&amp;b\">Save</Button>\n</Card>\n");

        var tag = JsonNode.Parse(Izzy(ItemTier.Account, "inspect", $"{page}:4:8", "--json").Out)!;
        Assert.Equal("Button", (string)tag["component"]!);
        var attributes = tag["attributes"]!.AsArray().ToDictionary(a => (string)a!["name"]!, a => a!);
        Assert.Equal("Outline", (string)attributes["Variant"]["value"]!);
        Assert.Equal("_size", (string)attributes["Size"]["expression"]!);
        Assert.Null(attributes["Variant"]["expression"]);
        Assert.True((bool)attributes["Submit"]["value"]!);
        Assert.Equal("a&b", (string)attributes["Href"]["value"]!);
        // From inside its content too.
        Assert.Contains("\"component\": \"Card\"", Izzy(ItemTier.Account, "inspect", $"{page}:6:1", "--json").Out);

        // A comment over two lines above a tag, with Windows line endings: the line under it is the tag's.
        File.WriteAllText(Path.Combine(_work, "Components/Pages/Notes.razor"), "@page \"/notes\"\r\n\r\n@* A sample page. Its headings\r\n   list themselves. *@\r\n<Column Gap=\"4\">\r\n</Column>\r\n");
        Assert.Contains("\"component\": \"Column\"", Izzy(ItemTier.Account, "inspect", "Components/Pages/Notes.razor:5:2", "--json").Out);

        // Signed out there is no panel; with an account it is read-only.
        Assert.Equal(4, Izzy(ItemTier.Free, "inspect", $"{page}:4:8", "--json").Exit);
        Assert.Equal(3, Izzy(ItemTier.Account, "set", $"{page}:4:8", "--variant", "Ghost").Exit);

        Assert.Equal(0, Izzy("set", $"{page}:4:8", "--variant", "ghost", "--full-width", "--unset", "submit").Exit);
        Assert.Contains("    <Button Variant=\"ButtonVariant.Ghost\" Size=\"@_size\"\n            Href=\"a&amp;b\" FullWidth>Save</Button>\n", File.ReadAllText(Path.Combine(_work, page)));
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
