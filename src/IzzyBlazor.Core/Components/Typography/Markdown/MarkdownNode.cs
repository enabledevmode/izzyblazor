using System.Text;

namespace IzzyBlazor;

/// <summary>What a node of a parsed Markdown document is.</summary>
internal enum MdType
{
    Document, BlockQuote, List, Item, Paragraph, Heading, ThematicBreak, CodeBlock, HtmlBlock,
    Table, TableRow, TableCell,
    Text, SoftBreak, LineBreak, Code, HtmlInline, Emph, Strong, Strike, Link, Image, TaskBox,
}

/// <summary>A list's marker: bullet or ordered, and how far its items' content is indented.</summary>
internal sealed class MdList
{
    public bool Ordered { get; init; }
    public bool Tight { get; set; } = true;
    public char Bullet { get; init; }
    public int Start { get; init; }
    public char Delimiter { get; init; }
    public int Padding { get; set; }
    public int MarkerOffset { get; init; }
}

/// <summary>A node of a parsed Markdown document: a block or an inline, in a tree.</summary>
internal sealed class MdNode(MdType type)
{
    public MdType Type { get; } = type;
    public MdNode? Parent { get; private set; }
    public MdNode? FirstChild { get; private set; }
    public MdNode? LastChild { get; private set; }
    public MdNode? Prev { get; private set; }
    public MdNode? Next { get; private set; }

    // Blocks: open while lines may still join them, and the lines they span.
    public bool Open { get; set; } = true;
    public int StartLine { get; set; }
    public int EndLine { get; set; }

    // A leaf block's raw text, before its inlines are parsed.
    public StringBuilder Content { get; set; } = new();

    public string Literal { get; set; } = "";
    public string Info { get; set; } = "";
    public int Level { get; set; }
    public bool Fenced { get; set; }
    public char FenceChar { get; set; }
    public int FenceLength { get; set; }
    public int FenceOffset { get; set; }
    public int HtmlBlockType { get; set; }
    public MdList? List { get; set; }
    public string Destination { get; set; } = "";
    public string Title { get; set; } = "";
    public bool Checked { get; set; }

    // Tables: each column's alignment ("left", "center", "right" or null); a row in the head.
    public List<string?>? Align { get; set; }
    public bool Head { get; set; }

    public static MdNode Text(string literal) => new(MdType.Text) { Literal = literal };

    public void Append(MdNode child)
    {
        child.Unlink();
        child.Parent = this;
        if (LastChild is { } last)
        {
            last.Next = child;
            child.Prev = last;
            LastChild = child;
        }
        else
        {
            FirstChild = LastChild = child;
        }
    }

    public void Prepend(MdNode child)
    {
        child.Unlink();
        child.Parent = this;
        if (FirstChild is { } first)
        {
            first.Prev = child;
            child.Next = first;
            FirstChild = child;
        }
        else
        {
            FirstChild = LastChild = child;
        }
    }

    public void InsertAfter(MdNode sibling)
    {
        sibling.Unlink();
        sibling.Next = Next;
        if (Next is not null) Next.Prev = sibling;
        sibling.Prev = this;
        Next = sibling;
        sibling.Parent = Parent;
        if (Parent is not null && sibling.Next is null) Parent.LastChild = sibling;
    }

    public void InsertBefore(MdNode sibling)
    {
        sibling.Unlink();
        sibling.Prev = Prev;
        if (Prev is not null) Prev.Next = sibling;
        sibling.Next = this;
        Prev = sibling;
        sibling.Parent = Parent;
        if (Parent is not null && sibling.Prev is null) Parent.FirstChild = sibling;
    }

    public void Unlink()
    {
        if (Prev is not null) Prev.Next = Next;
        else if (Parent is not null) Parent.FirstChild = Next;
        if (Next is not null) Next.Prev = Prev;
        else if (Parent is not null) Parent.LastChild = Prev;
        Parent = Prev = Next = null;
    }
}
