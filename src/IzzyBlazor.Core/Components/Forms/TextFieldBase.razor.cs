using System.ComponentModel;

namespace IzzyBlazor;

/// <summary>What <see cref="TextField"/> and <see cref="PasswordField"/> share. Not a component of its own.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract partial class TextFieldBase
{
    private protected TextFieldBase()
    {
    }
}
