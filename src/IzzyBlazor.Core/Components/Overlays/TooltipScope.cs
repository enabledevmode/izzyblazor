namespace IzzyBlazor;

/// <summary>Cascaded by a <see cref="Tooltip"/> to what's inside it, so an <see cref="IconButton"/> leaves out its own <c>title</c>.</summary>
internal sealed class TooltipScope
{
    public static readonly TooltipScope Instance = new();

    private TooltipScope()
    {
    }
}
