using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// What an editable <see cref="TileLayout"/> draws after its tiles, once their places are known:
/// the hidden input a static page posts the layout in, and the cells <c>ShowGrid</c> outlines.
/// </summary>
internal sealed class TileLayoutExtras : ComponentBase
{
    [Parameter, EditorRequired] public TileLayoutContext Context { get; set; } = null!;

    /// <summary>Changes after a move on an interactive page, so this draws again.</summary>
    [Parameter] public int Version { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var layout = Context.Owner;
        if (!layout.Editable)
        {
            return;
        }

        if (!layout.Live)
        {
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "type", "hidden");
            builder.AddAttribute(2, "name", $"{layout.Name}.Tiles");
            builder.AddAttribute(3, "value", Context.CurrentState().ToString());
            builder.CloseElement();
        }

        if (layout.ShowGrid)
        {
            var columns = layout.Levels[^1].Columns;
            var rows = Context.GridRows;
            for (var row = 1; row <= rows; row++)
            {
                for (var col = 1; col <= columns; col++)
                {
                    builder.OpenElement(4, "div");
                    builder.AddAttribute(5, "class", "ib-tiles-cell");
                    builder.AddAttribute(6, "style", string.Create(CultureInfo.InvariantCulture, $"--ib-cell-col:{col};--ib-cell-row:{row}"));
                    builder.AddAttribute(7, "aria-hidden", "true");
                    builder.CloseElement();
                }
            }
        }
    }
}
