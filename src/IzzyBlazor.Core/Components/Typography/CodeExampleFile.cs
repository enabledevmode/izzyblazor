using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// One source file of a <see cref="CodeExample"/>, shown as a tab over its source. Write it among the
/// example's content; it renders nothing where it's written.
/// </summary>
/// <example>
/// <code>
/// &lt;CodeExample&gt;
///     &lt;SignIn /&gt;
///     &lt;CodeExampleFile Title="SignIn.razor" Code="@markup" /&gt;
///     &lt;CodeExampleFile Title="SignIn.razor.cs" Language="csharp" Code="@code" /&gt;
/// &lt;/CodeExample&gt;
/// </code>
/// </example>
public sealed class CodeExampleFile : ComponentBase, IDisposable
{
    /// <summary>The file name, shown on its tab.</summary>
    [Parameter, EditorRequired] public string Title { get; set; } = "";

    /// <summary>The source.</summary>
    [Parameter, EditorRequired] public string Code { get; set; } = "";

    /// <summary>The language, for colors. Defaults to the example's <see cref="CodeExample.Language"/>.</summary>
    [Parameter] public string? Language { get; set; }

    /// <summary>Lines to highlight, such as <c>"3-5,9"</c> (see <see cref="IzzyBlazor.Code.HighlightLines"/>).</summary>
    [Parameter] public string? HighlightLines { get; set; }

    [CascadingParameter] private CodeExample? Example { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Example is null)
        {
            throw new InvalidOperationException("CodeExampleFile goes inside a CodeExample.");
        }

        Example.AddFile(this);
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => false;

    /// <inheritdoc />
    public void Dispose() => Example?.RemoveFile(this);
}
