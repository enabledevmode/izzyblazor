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

    /// <summary>A list of text styles: normal text and five heading levels from the editor's <c>TopHeading</c>.</summary>
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

    /// <summary>Add, change or remove a link (Ctrl+K).</summary>
    Link = 1 << 6,

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

    /// <summary>Align a paragraph or heading to the start, center or end (Ctrl+Shift+L, E, R).</summary>
    Alignment = 1 << 13,

    /// <summary>Subscript, as in H₂O (Ctrl+=).</summary>
    Subscript = 1 << 14,

    /// <summary>Superscript, as in x² (Ctrl+Shift+=).</summary>
    Superscript = 1 << 15,

    /// <summary>Text color, from a palette of seven that read in light and dark themes.</summary>
    TextColor = 1 << 16,

    /// <summary>A highlight behind the text, from the same palette.</summary>
    Highlight = 1 << 17,

    /// <summary>Text size: six steps from Smaller to Largest that scale with the text around them, or the editor's <c>FontSizes</c>.</summary>
    FontSize = 1 << 18,

    /// <summary>A font from the editor's <c>Fonts</c>; shows only when it has some.</summary>
    Font = 1 << 19,

    /// <summary>A quote: the paragraph, or the selected ones, set apart as quoted.</summary>
    Quote = 1 << 20,

    /// <summary>A code block, in a monospaced font, its spaces and lines kept.</summary>
    CodeBlock = 1 << 21,

    /// <summary>An image, by its address or uploaded (the editor's <c>ImageUploadUrl</c>), with its description.</summary>
    Image = 1 << 22,

    /// <summary>Tables: insert one, add and delete rows and columns, a header row.</summary>
    Table = 1 << 23,

    /// <summary>Bold, italic, links and the two lists: for short notes and comments.</summary>
    Basic = Bold | Italic | Link | BulletedList | NumberedList,

    /// <summary>Every tool (the default).</summary>
    Default = TextStyle | Bold | Italic | Underline | Strikethrough | InlineCode | Link | Image | BulletedList | NumberedList
        | Indent | Quote | CodeBlock | Table | HorizontalRule | ClearFormatting | UndoRedo,

    /// <summary>
    /// Every tool in <see cref="Default"/>, and alignment, subscript, superscript, text color,
    /// highlight, size and font: for pages people lay out, such as announcements and articles.
    /// </summary>
    Extended = Default | Alignment | Subscript | Superscript | TextColor | Highlight | FontSize | Font,
}
