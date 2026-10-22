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
        Assert.Contains("Buttons take a bare Submit", button.Properties.Single(p => p.Name == "Type").Obsolete); // Kept, marked.
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
        Assert.Equal(("none", "none", "full"), (onClick.Modes!.Off, onClick.Modes.Static, onClick.Modes.Live));
        Assert.Equal("full", button.Modes!.Off);
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
    public void Before_the_version_is_bumped_new_members_get_no_version()
    {
        var current = Api.File;
        var previous = current with
        {
            Components = current.Components.Select(c => c.Name == "Button" ? c with { Properties = c.Properties.Where(p => p.Name != "Href").ToList() } : c).ToList(),
        };

        var (file, _) = ReleaseComparer.Apply(current, previous);

        Assert.Null(file.Components.Single(c => c.Name == "Button").Properties.Single(p => p.Name == "Href").AddedIn);
    }

    [Fact]
    public void A_removed_overload_is_listed_by_its_signature()
    {
        ApiMember Method(string signature) => new() { Name = "Apply", Type = signature };
        ApiType Type(params ApiMember[] members) => new() { Name = "TableRequest", FullName = "IzzyBlazor.TableRequest`1", Kind = "record", Members = [.. members] };
        var empty = Api.File with { Components = [], Bases = [] };
        var previous = empty with { Version = "1.0.0", Types = [Type(Method("int Apply(int x)"), Method("int Apply(string s)"))] };
        var current = empty with { Version = "1.1.0", Types = [Type(Method("int Apply(int x)"))] };

        var (file, removed) = ReleaseComparer.Apply(current, previous);

        Assert.Equal(["TableRequest.Apply(string s)"], removed);
        Assert.Null(file.Types.Single().Members.Single().AddedIn);
    }

    [Fact]
    public void The_baseline_keeps_names_and_versions_only()
    {
        var baseline = ReleaseComparer.Baseline(Api.File);
        var button = baseline.Components.Single(c => c.Name == "Button");

        Assert.Null(button.Summary);
        Assert.All(button.Properties, p => Assert.Null(p.Summary));
        Assert.Equal(Api.Component("Button")!.Properties.Count, button.Properties.Count);
    }

    [Theory]
    [InlineData("P:IzzyBlazor.Button.Href", "Href")]
    [InlineData("F:IzzyBlazor.ButtonVariant.Solid", "ButtonVariant.Solid")]
    [InlineData("T:IzzyBlazor.FieldBase`1", "FieldBase<T>")]
    [InlineData("M:IzzyBlazor.Accent.Of(System.String)", "Of")]
    public void Links_read_like_code(string cref, string text) => Assert.Equal(text, XmlDocs.CrefText(cref));

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
    public void Usage_under_a_bullet_is_the_example_and_stays_out_of_the_list()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(root, "Sample.razor"), """
            @*
                A button that asks first. What confirming does:

                  - In a form: submits it.
                    <Sample Name="intent"
                            Value="delete" />
                  - Href: follows a link.
            *@
            """);

        var header = new RazorHeaders(root).For("Sample")!;

        Assert.Equal("<p>A button that asks first. What confirming does:</p>", header.Summary);
        Assert.Equal("<Sample Name=\"intent\"\n        Value=\"delete\" />", header.Example);
        Assert.Equal("<ul><li>In a form: submits it.</li><li>Href: follows a link.</li></ul>", header.Remarks);
    }

    [Fact]
    public void Header_text_after_the_example_is_kept()
    {
        var codeExample = Api.Component("CodeExample")!;
        var textField = Api.Component("TextField")!;

        Assert.Contains("Several files", codeExample.Remarks);
        Assert.Contains("<pre><code>&lt;TextField @bind-Value=&quot;Model.Website&quot;&gt;", textField.Remarks);
        Assert.StartsWith("<p>A styled EditForm. Works three ways:</p><ul><li>", Api.Component("Form")!.Summary);
    }

    [Theory]
    [InlineData("&quot;Close&quot;", "\"Close\"")]
    [InlineData("<code>Color.Surface</code>", "Color.Surface")]
    [InlineData("<a href=\"cref:F:IzzyBlazor.AvatarSize.Md\"><code>AvatarSize.Md</code></a>", "AvatarSize.Md")]
    [InlineData("1", "1")]
    [InlineData("the text color", null)]
    [InlineData("&quot;Info&quot;, &quot;Success&quot;", null)]
    [InlineData("<a href=\"cref:P:IzzyBlazor.AppSidebar.Position\"><code>Position</code></a>", null)]
    public void A_stated_default_is_shown_when_it_is_one_value(string html, string? expected) =>
        Assert.Equal(expected, ApiReader.Literal(html));

    [Fact]
    public void Defaults_filled_at_render_time_show_the_constant_the_docs_name()
    {
        var dialog = Api.Component("Dialog")!;
        var icon = Api.Component("Icon")!;

        Assert.Equal("\"Close\"", dialog.Properties.Single(p => p.Name == "CloseLabel").Default);
        Assert.Equal("computed", icon.Properties.Single(p => p.Name == "Color").Default); // "the surrounding text color"
    }

    [Fact]
    public void Shared_props_take_the_default_every_component_starts_with()
    {
        var item = Api.Base("LayoutItemBase")!;

        Assert.All(item.Properties, p => Assert.NotEqual("computed", p.Default));
        Assert.Contains(Api.File.Bases.SelectMany(b => b.Properties), p => p.Default is not null);
    }

    [Fact]
    public void Options_and_records_list_their_properties()
    {
        var options = Api.Type("ConfirmOptions")!;
        var request = Api.Type("TableRequest")!;
        var result = Api.Type("TableResult")!;

        Assert.Equal("Accent.Primary", options.Members.Single(m => m.Name == "Accent").Default);
        Assert.Contains("from 1", request.Members.Single(m => m.Name == "Page").Summary); // From the record's <param>.
        Assert.Contains(request.Members, m => m.Name == "ApplySorting" && m.Type.Contains("ApplySorting(", StringComparison.Ordinal));
        Assert.DoesNotContain(request.Members, m => m.Name is "Equals" or "Deconstruct" or "ToString");
        Assert.All(result.Members, m => Assert.NotNull(m.Summary));
    }

    [Fact]
    public void Removed_names_are_kept_with_their_message()
    {
        Assert.Contains("<Button Href", Api.Component("LinkButton")!.Obsolete);
        Assert.Null(Api.Component("Button")!.Obsolete);
        Assert.DoesNotContain("ButtonType", Api.Component("Button")!.Types); // Only obsolete members use it.
    }

    [Fact]
    public void Mode_support_is_not_inherited()
    {
        var usage = typeof(ModeSupportAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), false).Cast<AttributeUsageAttribute>().Single();

        Assert.False(usage.Inherited);
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Method));
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
        string[] everywhere = ["Form.OnValidSubmit", "Form.OnInvalidSubmit", "Form.OnSubmit"];
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
