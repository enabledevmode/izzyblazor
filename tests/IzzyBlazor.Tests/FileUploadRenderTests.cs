using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class FileUploadRenderTests : BunitContext
{
    public FileUploadRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    [Fact]
    public void Renders_a_named_file_input_in_a_drop_zone()
    {
        var cut = Render<FileUpload>(p => p
            .Add(f => f.Name, "Avatar")
            .Add(f => f.Label, "Profile photo")
            .Add(f => f.Accept, "image/*"));

        var input = cut.Find("ib-file-upload input[type=file]");
        var label = cut.Find("label");

        Assert.Equal("Avatar", input.GetAttribute("name"));
        Assert.Equal("image/*", input.GetAttribute("accept"));
        Assert.False(input.HasAttribute("multiple"));
        Assert.Equal(input.Id, label.GetAttribute("for"));
        Assert.Equal("Profile photo", label.TextContent);
    }

    [Fact]
    public void Multiple_required_and_disabled_reach_the_input()
    {
        var input = Render<FileUpload>(p => p
            .Add(f => f.Name, "Files")
            .Add(f => f.Multiple, true)
            .Add(f => f.Required, true)
            .Add(f => f.Disabled, true)).Find("input");

        Assert.True(input.HasAttribute("multiple"));
        Assert.True(input.HasAttribute("required"));
        Assert.True(input.HasAttribute("disabled"));
    }

    [Fact]
    public void Max_size_and_message_go_to_the_script()
    {
        var zone = Render<FileUpload>(p => p.Add(f => f.Name, "Avatar").Add(f => f.MaxFileSize, 2_000_000L)).Find("ib-file-upload");

        Assert.Equal("2000000", zone.GetAttribute("data-ib-max-size"));
        Assert.Equal("{0} is larger than {1}.", zone.GetAttribute("data-ib-too-large"));
        Assert.Equal("{0} isn't an accepted file type.", zone.GetAttribute("data-ib-wrong-type"));
    }

    [Fact]
    public void Errors_mark_the_field_invalid_and_describe_the_input()
    {
        var cut = Render<FileUpload>(p => p
            .Add(f => f.Name, "Avatar")
            .Add(f => f.Hint, "Up to 2 MB.")
            .Add(f => f.Errors, new[] { "Choose a photo." }));

        var input = cut.Find("input");

        Assert.Contains("ib-field-invalid", cut.Find(".ib-field").ClassList);
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.Equal($"{input.Id}-hint {input.Id}-error", input.GetAttribute("aria-describedby"));
        Assert.Contains("Choose a photo.", cut.Find(".ib-field-error").TextContent);
    }

    [Fact]
    public void Choose_link_is_a_label_for_the_input()
    {
        var cut = Render<FileUpload>(p => p.Add(f => f.Name, "A"));
        var input = cut.Find("input[type=file]");
        var choose = cut.Find(".ib-file-choose");

        Assert.Equal("label", choose.TagName, ignoreCase: true);
        Assert.Equal(input.Id, choose.GetAttribute("for"));
        Assert.Equal("Choose a file", choose.TextContent);
        Assert.Equal("or drag it here", cut.Find(".ib-file-drag").TextContent);
    }

    [Fact]
    public void Prompt_is_plural_with_multiple_and_can_be_replaced()
    {
        var multiple = Render<FileUpload>(p => p.Add(f => f.Name, "A").Add(f => f.Multiple, true));
        Assert.Equal("Choose files", multiple.Find(".ib-file-choose").TextContent);
        Assert.Equal("or drag them here", multiple.Find(".ib-file-drag").TextContent);

        var custom = Render<FileUpload>(p => p.Add(f => f.Name, "A").Add(f => f.ChooseText, "Browse").Add(f => f.DragText, "or drop a photo"));
        Assert.Equal("Browse", custom.Find(".ib-file-choose").TextContent);
        Assert.Equal("or drop a photo", custom.Find(".ib-file-drag").TextContent);
    }

    [Fact]
    public void Input_is_named_by_the_field_label_not_the_link()
    {
        var cut = Render<FileUpload>(p => p.Add(f => f.Name, "A").Add(f => f.Label, "New photo"));
        var input = cut.Find("input[type=file]");
        var label = cut.Find(".ib-field-label");

        Assert.Equal(label.Id, input.GetAttribute("aria-labelledby"));
        Assert.False(Render<FileUpload>(p => p.Add(f => f.Name, "A")).Find("input").HasAttribute("aria-labelledby"));
    }

    [Theory]
    [InlineData(".png,.jpg", 2_000_000L, "PNG or JPG · up to 2 MB")]
    [InlineData("image/png, image/jpeg, image/svg+xml", null, "PNG, JPEG or SVG")]
    [InlineData(".pdf", 1_536L, "PDF · up to 1.5 KB")]
    [InlineData(".pdf", 1_250_000L, "PDF · up to 1.3 MB")] // rounds half up, as the script's messages do
    [InlineData("image/*", 5_000_000L, "up to 5 MB")]
    [InlineData(".png,application/vnd.ms-excel", null, null)]
    [InlineData(null, null, null)]
    public void Hint_comes_from_accept_and_max_size(string? accept, long? max, string? expected)
    {
        var cut = Render<FileUpload>(p => p.Add(f => f.Name, "A").Add(f => f.Accept, accept).Add(f => f.MaxFileSize, max));
        var hint = cut.FindAll(".ib-file-accept");

        if (expected is null) Assert.Empty(hint);
        else Assert.Equal(expected, Assert.Single(hint).TextContent);
    }

    [Fact]
    public void Hint_describes_the_input()
    {
        var input = Render<FileUpload>(p => p.Add(f => f.Name, "A").Add(f => f.Accept, ".pdf").Add(f => f.Hint, "One file.")).Find("input");
        Assert.Equal($"{input.Id}-accept {input.Id}-hint", input.GetAttribute("aria-describedby"));
    }

    [Fact]
    public void AcceptText_replaces_the_hint_and_empty_hides_it()
    {
        Assert.Equal("Square photos", Render<FileUpload>(p => p.Add(f => f.Name, "A").Add(f => f.Accept, ".png").Add(f => f.AcceptText, "Square photos")).Find(".ib-file-accept").TextContent);
        Assert.Empty(Render<FileUpload>(p => p.Add(f => f.Name, "A").Add(f => f.Accept, ".png").Add(f => f.AcceptText, "")).FindAll(".ib-file-accept"));
    }

    [Fact]
    public void HideLabel_keeps_the_label_for_screen_readers()
    {
        var label = Render<FileUpload>(p => p.Add(f => f.Name, "A").Add(f => f.Label, "Photo").Add(f => f.HideLabel, true)).Find(".ib-field-label");
        Assert.Contains("ib-visually-hidden", label.ClassList);
    }

    [Fact]
    public void Interactive_pages_use_InputFile_with_the_same_attributes()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<FileUpload>(p => p.Add(f => f.Name, "Avatar").Add(f => f.Accept, "image/*"));
        var input = cut.Find("ib-file-upload input[type=file]");

        Assert.Single(cut.FindComponents<Microsoft.AspNetCore.Components.Forms.InputFile>());
        Assert.Equal("Avatar", input.GetAttribute("name"));
        Assert.Equal("image/*", input.GetAttribute("accept"));
        Assert.Contains("ib-file-input", input.ClassList);
    }

    [Fact]
    public void Interactive_OnChange_trims_to_MaxFiles_and_leaves_out_oversized_files()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        IReadOnlyList<Microsoft.AspNetCore.Components.Forms.IBrowserFile>? received = null;

        var cut = Render<FileUpload>(p => p
            .Add(f => f.Name, "Files")
            .Add(f => f.Multiple, true)
            .Add(f => f.MaxFiles, 3)
            .Add(f => f.MaxFileSize, 10L)
            .Add(f => f.OnChange, files => received = files));

        var big = InputFileContent.CreateFromText(new string('x', 50), "big.txt");
        var small = Enumerable.Range(1, 12).Select(i => InputFileContent.CreateFromText("hi", $"f{i}.txt"));
        cut.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles([big, .. small]);

        Assert.NotNull(received);
        Assert.Equal(new[] { "f1.txt", "f2.txt", "f3.txt" }, received!.Select(f => f.Name).ToArray());
    }

    [Fact]
    public void Interactive_zone_skips_the_multipart_warning()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        JSInterop.Mode = JSRuntimeMode.Loose;

        Assert.True(Render<FileUpload>(p => p.Add(f => f.Name, "A")).Find("ib-file-upload").HasAttribute("data-ib-interactive"));
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        Assert.False(Render<FileUpload>(p => p.Add(f => f.Name, "A")).Find("ib-file-upload").HasAttribute("data-ib-interactive"));
    }

    [Fact]
    public void Requires_a_name()
    {
        Assert.ThrowsAny<InvalidOperationException>(() => Render<FileUpload>());
    }

    [Fact]
    public void Multipart_form_sets_the_enctype()
    {
        var form = Render<Form>(p => p.Add(f => f.Model, new object()).Add(f => f.Multipart, true)).Find("form");

        Assert.Equal("multipart/form-data", form.GetAttribute("enctype"));
    }

    [Fact]
    public void Plain_form_has_no_enctype()
    {
        Assert.False(Render<Form>(p => p.Add(f => f.Model, new object())).Find("form").HasAttribute("enctype"));
    }
}
