using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A row and what follows it (its detail), kept together by <c>@key</c> where rows move: a key on
/// the row alone, inside the fragment that draws it, would only be compared with the row at the
/// same place, so a moved row's detail would stay behind.
/// </summary>
internal sealed class KeyedRows : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);
}
