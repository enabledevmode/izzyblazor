using System.ComponentModel;

namespace IzzyBlazor;

/// <summary>What <see cref="MonthField{TValue}"/> and <see cref="YearField{TValue}"/> share. Not a component of its own.</summary>
/// <typeparam name="TValue">The bound type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract partial class MonthYearFieldBase<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] TValue>
{
    private protected MonthYearFieldBase()
    {
    }
}
