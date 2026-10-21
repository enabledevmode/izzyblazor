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
        Assert.DoesNotContain("Type", names); // Obsolete as an error.
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

    [Theory]
    [InlineData("P:IzzyBlazor.Button.Href", "Href")]
    [InlineData("F:IzzyBlazor.ButtonVariant.Solid", "ButtonVariant.Solid")]
    [InlineData("T:IzzyBlazor.FieldBase`1", "FieldBase<T>")]
    [InlineData("M:IzzyBlazor.Accent.Of(System.String)", "Of")]
    public void Links_read_like_code(string cref, string text) => Assert.Equal(text, XmlDocs.CrefText(cref));
}
