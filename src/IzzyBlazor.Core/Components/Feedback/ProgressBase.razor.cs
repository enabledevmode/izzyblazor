using System.ComponentModel;

namespace IzzyBlazor;

/// <summary>What <see cref="Progress"/> and <see cref="ProgressCircle"/> share. Not a component of its own.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract partial class ProgressBase
{
    private protected ProgressBase()
    {
    }
}
