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
        Assert.True((bool)attributes["Size"]["expression"]!);
        Assert.True((bool)attributes["Submit"]["value"]!);
        Assert.Equal("a&b", (string)attributes["Href"]["value"]!);
        // From inside its content too.
        Assert.Contains("\"component\": \"Card\"", Izzy(ItemTier.Account, "inspect", $"{page}:6:1", "--json").Out);

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
