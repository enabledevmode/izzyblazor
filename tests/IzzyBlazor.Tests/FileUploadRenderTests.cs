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
    public void Prompt_is_singular_unless_multiple()
    {
        Assert.Equal("Drag a file here, or", Render<FileUpload>(p => p.Add(f => f.Name, "A")).Find(".ib-file-prompt").TextContent);
        Assert.Equal("Drag files here, or", Render<FileUpload>(p => p.Add(f => f.Name, "A").Add(f => f.Multiple, true)).Find(".ib-file-prompt").TextContent);
        Assert.Equal("Drop it here", Render<FileUpload>(p => p.Add(f => f.Name, "A").Add(f => f.PromptText, "Drop it here")).Find(".ib-file-prompt").TextContent);
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
