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
    public void Input_is_named_by_the_field_label_not_the_link()
    {
        var cut = Render<FileUpload>(p => p.Add(f => f.Name, "A").Add(f => f.Label, "New photo"));
        var input = cut.Find("input[type=file]");
        var label = cut.Find(".ib-field-label");

        Assert.Equal(label.Id, input.GetAttribute("aria-labelledby"));
        Assert.False(Render<FileUpload>(p => p.Add(f => f.Name, "A")).Find("input").HasAttribute("aria-labelledby"));
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
    public void Multipart_form_sets_the_enctype()
    {
        var form = Render<Form>(p => p.Add(f => f.Model, new object()).Add(f => f.Multipart, true)).Find("form");

        Assert.Equal("multipart/form-data", form.GetAttribute("enctype"));
    }
}
