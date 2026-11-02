using IzzyBlazor.DocsGen;
using IzzyBlazor.Site.Services;

namespace IzzyBlazor.Site.Tests;

// What the generator reads from the library: Button's API page has no hand-written table, so
// this is what it shows.
public class DocsGenTests
{
    private static readonly ApiMetadata Api = ApiMetadata.Load();

    [Fact]
    public void Button_lists_its_own_parameters_with_defaults_and_docs()
    {
        var button = Api.Component("Button")!;
        var names = button.Properties.Select(p => p.Name).ToList();

        Assert.Contains("Href", names);
        Assert.Contains("Submit", names);
        Assert.Contains("Accent", names); // From ButtonBase, listed as its own.
        Assert.DoesNotContain("Margin", names); // Item props are listed once, under LayoutItemBase.
        Assert.DoesNotContain("Type", names); // Not a parameter any more.
        Assert.Equal(["LayoutItemBase", "IzzyComponentBase"], button.Bases);

        var accent = button.Properties.Single(p => p.Name == "Accent");
        Assert.Equal("Accent.Primary", accent.Default);
        Assert.Equal("ButtonBase", accent.DeclaredOn);
        Assert.Equal("false", button.Properties.Single(p => p.Name == "Submit").Default);
        Assert.Contains("cref:P:IzzyBlazor.Button.Submit", button.Properties.Single(p => p.Name == "Href").Summary);
    }

    [Fact]
    public void Events_and_mode_support_come_from_the_attribute()
    {
        var button = Api.Component("Button")!;
        var onClick = Assert.Single(button.Events);

        Assert.Equal("OnClick", onClick.Name);
        Assert.Equal("EventCallback<MouseEventArgs>", onClick.Type);
        Assert.Equal(("none", "none", "full"), (onClick.Modes!.NoScript, onClick.Modes.Static, onClick.Modes.Interactive));
        Assert.Equal("full", button.Modes!.NoScript);
    }

    [Fact]
    public void Bind_pairs_read_as_one_bindable_property()
    {
        var field = Api.Component("TextField")!;

        Assert.True(field.Properties.Single(p => p.Name == "Value").Bindable);
        Assert.DoesNotContain(field.Events, e => e.Name == "ValueChanged");
        Assert.DoesNotContain(field.Properties, p => p.Name == "ValueExpression");
    }

    [Fact]
    public void A_new_release_marks_what_was_added_since_the_last()
    {
        var current = Api.File;
        var previous = current with
        {
            Version = "0.1.0-alpha.11",
            Components = current.Components
                .Where(c => c.Name != "PrevNext")
                .Select(c => c.Name == "Button" ? c with { Properties = c.Properties.Where(p => p.Name != "Href").ToList() } : c)
                .ToList(),
        };

        var (file, _) = ReleaseComparer.Apply(current, previous);
        var button = file.Components.Single(c => c.Name == "Button");

        Assert.Equal(current.Version, file.Components.Single(c => c.Name == "PrevNext").AddedIn);
        Assert.Equal(current.Version, button.Properties.Single(p => p.Name == "Href").AddedIn);
        Assert.Null(button.Properties.Single(p => p.Name == "Submit").AddedIn);
    }

    [Fact]
    public void A_razor_header_gives_a_summary_an_example_and_remarks()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(root, "Sample.razor"), """
            @namespace IzzyBlazor
            @inherits FieldBase<bool>

            @*
                A sample with a `Value` and a native <details>.
                It wraps.
                <Sample @bind-Value="Model.On" />
                <Sample Disabled />

                Works three ways:
                  - No script: a checkbox,
                    wrapped.
                  - With script: more.

                A trigger takes the attributes:
                <Sample>
                    ...
                </Sample>
            *@

            <div></div>
            """);

        var header = new RazorHeaders(root).For("Sample")!;

        Assert.Equal("<p>A sample with a <code>Value</code> and a native <code>&lt;details&gt;</code>. It wraps.</p>", header.Summary);
        Assert.Equal("<Sample @bind-Value=\"Model.On\" />\n<Sample Disabled />", header.Example);
        Assert.Equal(
            "<p>Works three ways:</p><ul><li>No script: a checkbox, wrapped.</li><li>With script: more.</li></ul>" +
            "<p>A trigger takes the attributes:</p><pre><code>&lt;Sample&gt;\n    ...\n&lt;/Sample&gt;</code></pre>",
            header.Remarks);
    }

    [Fact]
    public void A_components_keys_are_grouped_by_where_focus_is_and_a_screen_lists_them()
    {
        var keyboard = Api.Component("DataGrid")!.Keyboard!;
        Assert.Equal(["In the rows", "Inside a cell", "Selecting rows", "On a header", "In the footer", "In the filter row", "Inside a filter cell", "In a filter panel", "In a filter's list"], keyboard.Select(g => g.Title));
        var home = keyboard[0].Rows.Single(r => r.Keys.Contains("Ctrl+Home"));
        Assert.Equal(["Ctrl+Home", "Ctrl+End"], home.Keys);
        Assert.Equal("Move to the first cell of the first row, or the last cell of the last", home.Action);
        Assert.DoesNotContain("Ctrl+Home", Api.Component("DataGrid")!.Accessibility);
    }

    private static RazorHeaders.Header? KeysHeader(string keyboard)
    {
        var folder = Directory.CreateTempSubdirectory("izzy-keys");
        try
        {
            File.WriteAllText(Path.Combine(folder.FullName, "Thing.razor"), $"@* \n    Keyboard:\n{keyboard}\n*@\n");
            return new RazorHeaders(folder.FullName).For("Thing");
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void Every_documented_component_has_a_summary()
    {
        var api = IzzyBlazor.Site.Services.ApiMetadata.Load();
        var registry = IzzyBlazor.Site.Services.Registry.Load(api);
        var missing = registry.Components.Where(c => api.Component(c.Name)?.Summary is null).Select(c => c.Name).ToList();

        Assert.True(missing.Count == 0, $"No summary: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_documented_component_records_its_mode_support()
    {
        var api = IzzyBlazor.Site.Services.ApiMetadata.Load();
        var registry = IzzyBlazor.Site.Services.Registry.Load(api);
        var missing = registry.Components.Where(c => api.Component(c.Name)?.Modes is null).Select(c => c.Name).ToList();

        Assert.True(missing.Count == 0, $"Add [ModeSupport] to: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_callback_says_where_it_runs()
    {
        // C# callbacks run only on interactive pages, and the API pages label them so. The
        // exceptions are what a static post does too: a form's submit events, and a field's
        // @bind-Value (the model binds from the posted form).
        string[] everywhere = ["Form.OnValidSubmit", "Form.OnInvalidSubmit", "Form.OnSubmit", "DataGrid.OnSave"];
        var unmarked = Api.File.Components
            .SelectMany(c => c.Events.Where(e => e.Modes is null).Select(e => $"{c.Name}.{e.Name}")
                .Concat(c.Properties
                    .Where(p => p.Bindable && p.BindModes is null && !(p.Name == "Value" && c.Bases.Contains("FieldBase")))
                    .Select(p => $"{c.Name}.{p.Name}Changed")))
            .Except(everywhere)
            .ToList();

        Assert.True(unmarked.Count == 0, $"Add [ModeSupport] to: {string.Join(", ", unmarked)}");
    }
}
