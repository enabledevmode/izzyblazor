using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class RichTextEditorTests : BunitContext
{
    public class Model
    {
        public string? Body { get; set; }
    }

    public RichTextEditorTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    // A static post binds the model straight from the form, so the field cleans it before the
    // form's submit handler sees it: what reaches the model is always sanitized.
    [Fact]
    public void A_posted_value_binds_sanitized_and_renders_as_text_with_its_html_beside_it()
    {
        var model = new Model { Body = "<h1 onclick=x>Hi</h1><script>alert(1)</script><p>there</p>" };
        var cut = Render<RichTextEditor>(p => p
            .AddCascadingValue(new EditContext(model))
            .Add(f => f.ValueExpression, () => model.Body)
            .Add(f => f.Value, model.Body)
            .Add(f => f.ValueChanged, v => model.Body = v)
            .Add(f => f.Label, "Body"));

        Assert.Equal("<h2>Hi</h2><p>there</p>", model.Body);

        var surface = cut.Find(".ib-rich-text-surface");
        Assert.Equal("textbox", surface.GetAttribute("role"));
        Assert.Equal("true", surface.GetAttribute("aria-multiline"));
        Assert.Equal(cut.Find("label").Id, surface.GetAttribute("aria-labelledby"));
        Assert.Equal("<h2>Hi</h2><p>there</p>", surface.InnerHtml);

        var source = cut.Find("textarea.ib-rich-text-source");
        Assert.Equal("model.Body", source.GetAttribute("name"));
        Assert.Equal(cut.Find("label").GetAttribute("for"), source.Id);
        Assert.Equal("<h2>Hi</h2><p>there</p>", source.GetAttribute("value") ?? source.TextContent);
    }

    // An editor left empty (an empty paragraph) binds null, so [Required] sees it as empty.
    [Fact]
    public void An_empty_editor_binds_null()
    {
        var model = new Model { Body = "<p><br></p>" };
        Render<RichTextEditor>(p => p
            .AddCascadingValue(new EditContext(model))
            .Add(f => f.ValueExpression, () => model.Body)
            .Add(f => f.Value, model.Body)
            .Add(f => f.ValueChanged, v => model.Body = v));

        Assert.Null(model.Body);
    }

    // MaxLength counts the text readers see, not the HTML around it.
    [Fact]
    public void Max_length_counts_text_not_html()
    {
        var model = new Model { Body = "<p><strong>12345</strong></p><p>6789&amp;</p>" };
        var context = new EditContext(model);
        var cut = Render<RichTextEditor>(p => p
            .AddCascadingValue(context)
            .Add(f => f.ValueExpression, () => model.Body)
            .Add(f => f.Value, model.Body)
            .Add(f => f.ValueChanged, v => model.Body = v)
            .Add(f => f.MaxLength, 10));

        Assert.True(context.Validate());
        cut.Render(p => p.Add(f => f.Value, "<p>12345678901</p>"));
        Assert.False(context.Validate());
        Assert.Equal(["Body must be 10 characters or fewer."], context.GetValidationMessages());
    }

    [Fact]
    public void Tools_show_their_shortcuts_and_a_read_only_editor_has_none()
    {
        var model = new Model { Body = "<p>a</p>" };
        var cut = Render<RichTextEditor>(p => p.Add(f => f.ValueExpression, () => model.Body).Add(f => f.Value, model.Body)
            .Add(f => f.Label, "Notes").Add(f => f.Tools, RichTextTools.Basic));

        var bold = cut.Find("[data-ib-command=bold]");
        Assert.Equal("Bold", bold.GetAttribute("aria-label"));
        Assert.Equal("false", bold.GetAttribute("aria-pressed"));
        Assert.Equal("Control+B", bold.GetAttribute("aria-keyshortcuts"));
        Assert.Equal("Bold (Ctrl+B)", bold.GetAttribute("title"));
        Assert.Equal(5, cut.FindAll("[data-ib-command]").Count);

        cut.Render(p => p.Add(f => f.ReadOnly, true));
        Assert.Empty(cut.FindAll("[data-ib-command]"));
        Assert.Equal("true", cut.Find(".ib-rich-text-surface").GetAttribute("aria-readonly"));
    }
}
