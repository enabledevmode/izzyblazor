namespace IzzyBlazor;

/// <summary>
/// The tools in a <see cref="RichTextEditor"/>'s toolbar. Combine single tools with <c>|</c>, or
/// start from <see cref="Basic"/> or <see cref="Default"/>.
/// </summary>
/// <remarks>
/// Keyboard shortcuts work for every format the editor allows, whether or not its tool shows.
/// </remarks>
[Flags]
public enum RichTextTools
{
    /// <summary>No toolbar: shortcuts only.</summary>
    None = 0,

    /// <summary>A list of text styles: paragraph, headings 2 to 4, quote and code block.</summary>
    TextStyle = 1 << 0,

    /// <summary>Bold (Ctrl+B).</summary>
    Bold = 1 << 1,

    /// <summary>Italic (Ctrl+I).</summary>
    Italic = 1 << 2,

    /// <summary>Underline (Ctrl+U).</summary>
    Underline = 1 << 3,

    /// <summary>Strikethrough (Ctrl+Shift+X).</summary>
    Strikethrough = 1 << 4,

    /// <summary>Inline code (Ctrl+E).</summary>
    InlineCode = 1 << 5,

    /// <summary>Bulleted list (Ctrl+Shift+8).</summary>
    BulletedList = 1 << 7,

    /// <summary>Numbered list (Ctrl+Shift+7).</summary>
    NumberedList = 1 << 8,

    /// <summary>Indent and outdent a list item (Ctrl+] and Ctrl+[).</summary>
    Indent = 1 << 9,

    /// <summary>A horizontal line between sections.</summary>
    HorizontalRule = 1 << 10,

    /// <summary>Removes bold, italic and the other text formats from the selection.</summary>
    ClearFormatting = 1 << 11,

    /// <summary>Undo (Ctrl+Z) and redo (Ctrl+Y).</summary>
    UndoRedo = 1 << 12,

    /// <summary>Bold, italic and the two lists: for short notes and comments.</summary>
    Basic = Bold | Italic | BulletedList | NumberedList,

    /// <summary>Every tool (the default).</summary>
    Default = TextStyle | Bold | Italic | Underline | Strikethrough | InlineCode | BulletedList | NumberedList
        | Indent | HorizontalRule | ClearFormatting | UndoRedo,
}
