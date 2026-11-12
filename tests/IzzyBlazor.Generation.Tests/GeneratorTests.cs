using System.Text;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

public class GeneratorTests
{
    const string Manifest = """
        {
          "id": "probe", "name": "Probe", "kind": "starter",
          "parameters": {
            "type": "object",
            "properties": {
              "Flag": { "type": "boolean", "default": false },
              "Mode": { "type": "string", "enum": ["None", "Server", "Auto"], "default": "Server" },
              "PageName": { "type": "string", "default": "Home" }
            }
          },
          "computed": { "Interactive": "Mode != \"None\"" },
          "exclude": [ { "if": "!Interactive", "paths": ["Pages/Counter.razor", "Client/**"] } ],
          "packages": [ { "id": "IzzyBlazor", "version": "0.1.0" }, { "id": "Extra", "version": "1.0.0", "if": "Flag" } ]
        }
        """;

    static ItemDefinition Item(params (string Path, string Text)[] files) => new()
    {
        Manifest = ItemManifest.Parse(Manifest),
        Files = files.ToDictionary(f => f.Path, f => Encoding.UTF8.GetBytes(f.Text)),
    };

    static Changeset Run(ItemDefinition item, params (string Key, string Value)[] settings) =>
        Generator.Generate(item, settings.ToDictionary(s => s.Key, s => s.Value), ProjectInfo.ForNewProject("MyApp"));

    static string Text(Changeset c, string path) => c.Creates.Single(f => f.Path == path).Text!;

    // The same files and results as the dotnet new probe (Nov 12): each file type's comment syntax, elif and else.
    [Fact]
    public void Conditions_in_each_file_type_match_dotnet_new()
    {
        var item = Item(
            ("Page.razor", "@* top *@\n@*#if (Flag) *@\n<p>flag on</p>\n@*#elif (Mode == \"Auto\") *@\n<p>auto</p>\n@*#else *@\n<p>else</p>\n@*#endif *@\n<h1>{{PageName}}</h1>\n"),
            ("Code.cs", "namespace {{RootNamespace}};\n#if (Flag)\nclass A {}\n#elif (Mode == \"None\" || !Flag)\nclass B {}\n#endif\nclass C {}\n"),
            ("site.css", "/*#if (Flag) */\n.a{}\n/*#else */\n.b{}\n/*#endif */\n"),
            ("app.json", "{\n  //#if (Flag)\n  \"a\": 1,\n  //#endif\n  \"b\": 2\n}\n"),
            ("p.csproj", "<!--#if (Flag) -->\n<x/>\n<!--#endif -->\n<y/>\n"));

        var off = Run(item);
        Assert.Equal("@* top *@\n<p>else</p>\n<h1>Home</h1>\n", Text(off, "Page.razor"));
        Assert.Equal("namespace MyApp;\nclass B {}\nclass C {}\n", Text(off, "Code.cs"));
        Assert.Equal(".b{}\n", Text(off, "site.css"));
        Assert.Equal("{\n  \"b\": 2\n}\n", Text(off, "app.json"));
        Assert.Equal("<y/>\n", Text(off, "p.csproj"));

        var on = Run(item, ("Flag", "true"));
        Assert.Equal("@* top *@\n<p>flag on</p>\n<h1>Home</h1>\n", Text(on, "Page.razor"));
        Assert.Equal("namespace MyApp;\nclass A {}\nclass C {}\n", Text(on, "Code.cs"));
        Assert.Equal(".a{}\n", Text(on, "site.css"));
        Assert.Equal("{\n  \"a\": 1,\n  \"b\": 2\n}\n", Text(on, "app.json"));
        Assert.Equal("<x/>\n<y/>\n", Text(on, "p.csproj"));

        var auto = Run(item, ("Mode", "auto"), ("PageName", "Dash"));
        Assert.Equal("@* top *@\n<p>auto</p>\n<h1>Dash</h1>\n", Text(auto, "Page.razor"));
    }

    // dotnet new's results for these names (Nov 12 probe, derived symbol with safe_namespace).
    [Theory]
    [InlineData("MyApp", "MyApp")]
    [InlineData("My-App.2", "My_App._2")]
    [InlineData("9 lives", "_9_lives")]
    [InlineData("Über.App", "Über.App")]
    public void A_project_name_gives_the_namespace_dotnet_new_gives(string name, string expected) =>
        Assert.Equal(expected, Names.SafeNamespace(name));

    [Fact]
    public void Excludes_packages_and_tokens_in_paths_follow_the_settings()
    {
        var item = Item(
            ("Pages/{{PageName}}.razor", "<h1>{{PageName}}</h1>\n"),
            ("Pages/Counter.razor", "counter\n"),
            ("Client/Program.cs", "client\n"));

        var server = Run(item, ("PageName", "Dash"));
        Assert.Equal(["Client/Program.cs", "Pages/Counter.razor", "Pages/Dash.razor"], server.Creates.Select(f => f.Path));
        Assert.Equal(["IzzyBlazor"], server.Packages.Select(p => p.Id));

        var none = Run(item, ("Mode", "None"), ("Flag", "true"));
        Assert.Equal(["Pages/Home.razor"], none.Creates.Select(f => f.Path));
        Assert.Equal(["IzzyBlazor", "Extra"], none.Packages.Select(p => p.Id));
    }

    [Fact]
    public void Wrong_settings_say_what_is_allowed()
    {
        var item = Item(("a.razor", "x\n"));
        Assert.Contains("Settings: Flag, Mode, PageName", Assert.Throws<GenerationException>(() => Run(item, ("Colour", "red"))).Message);
        Assert.Contains("one of None, Server, Auto", Assert.Throws<GenerationException>(() => Run(item, ("Mode", "Wasm"))).Message);
        Assert.Contains("true or false", Assert.Throws<GenerationException>(() => Run(item, ("Flag", "yes"))).Message);
    }
}
