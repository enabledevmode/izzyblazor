using System.ComponentModel;

namespace IzzyBlazor;

/// <summary>What <see cref="TreeSelect{TValue, TItem}"/> and the multiple one share. Not a component of its own.</summary>
/// <typeparam name="TValue">The bound type.</typeparam>
/// <typeparam name="TItem">The tree's items.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract partial class TreeSelectBase<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] TValue, TItem>
{
    private protected TreeSelectBase()
    {
    }
}
