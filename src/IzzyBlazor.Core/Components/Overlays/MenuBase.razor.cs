using System.ComponentModel;

namespace IzzyBlazor;

/// <summary>What <see cref="Menu"/> and <see cref="ContextMenu"/> share: the panel of items. Not a component of its own.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract partial class MenuBase
{
    private protected MenuBase()
    {
    }
}
