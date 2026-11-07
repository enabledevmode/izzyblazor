using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor;

/// <summary>
/// Markdown's blocks (CommonMark 0.31.2, with GitHub-flavored tables and task lists), read line by
/// line into a tree: the algorithm of the reference parser, commonmark.js. Its inlines are parsed
/// after, by <see cref="MdInlines"/>.
/// </summary>
internal sealed partial class MdBlocks
{
    private const int CodeIndent = 4;

    private readonly MdNode _doc = new(MdType.Document) { StartLine = 1 };
    private readonly Dictionary<string, (string Destination, string Title)> _refs = new(StringComparer.Ordinal);
    private MdNode? _tip;
    private MdNode _oldTip = null!;
    private MdNode _lastMatched = null!;
    private string _line = "";
    private int _lineNumber;
    private int _offset;
    private int _column;
    private int _nextNonspace;
    private int _nextNonspaceColumn;
    private int _indent;
    private int _lastLineLength;
    private bool _indented;
    private bool _blank;
    private bool _partiallyConsumedTab;
    private bool _allClosed = true;
    private const int MaxColumns = 128;
    private int _filledCells;
    private int _cellBudget;

    [GeneratedRegex(@"^#{1,6}(?:[ \t]+|$)")]
    private static partial Regex AtxHeadingMarker();

    [GeneratedRegex(@"^`{3,}(?!.*`)|^~{3,}")]
    private static partial Regex CodeFence();

    [GeneratedRegex(@"^(?:`{3,}|~{3,})(?=[ \t]*$)")]
    private static partial Regex ClosingCodeFence();

    [GeneratedRegex(@"^(?:=+|-+)[ \t]*$")]
    private static partial Regex SetextHeadingLine();

    [GeneratedRegex(@"^(?:\*[ \t]*){3,}$|^(?:_[ \t]*){3,}$|^(?:-[ \t]*){3,}$")]
    private static partial Regex ThematicBreak();

    [GeneratedRegex(@"^(\d{1,9})([.)])")]
    private static partial Regex OrderedListMarker();

    [GeneratedRegex(@"^[ \t]*#+[ \t]*$")]
    private static partial Regex HeadingOnlyHashes();

    [GeneratedRegex(@"[ \t]+#+[ \t]*$")]
    private static partial Regex HeadingClosingHashes();

    [GeneratedRegex(@"^:?-+:?$")]
    private static partial Regex DelimiterCell();

    [GeneratedRegex(@"^\[([ \txX])\](?=[ \t\n])")]
    private static partial Regex TaskMarker();

    private static readonly Regex[] HtmlBlockOpen =
    [
        new("."),
        new(@"^<(?:script|pre|textarea|style)(?:\s|>|$)", RegexOptions.IgnoreCase),
        new("^<!--"),
        new(@"^<[?]"),
        new("^<![A-Za-z]"),
        new(@"^<!\[CDATA\["),
        new(@"^<[/]?(?:address|article|aside|base|basefont|blockquote|body|caption|center|col|colgroup|dd|details|dialog|dir|div|dl|dt|fieldset|figcaption|figure|footer|form|frame|frameset|h[123456]|head|header|hr|html|iframe|legend|li|link|main|menu|menuitem|nav|noframes|ol|optgroup|option|p|param|search|section|summary|table|tbody|td|tfoot|th|thead|title|tr|track|ul)(?:\s|[/]?[>]|$)", RegexOptions.IgnoreCase),
        new("^(?:" + MdSyntax.OpenTag + "|" + MdSyntax.CloseTag + @")\s*$", RegexOptions.IgnoreCase),
    ];

    private static readonly Regex[] HtmlBlockClose =
    [
        new("."),
        new("</(?:script|pre|textarea|style)>", RegexOptions.IgnoreCase),
        new("-->"),
        new(@"\?>"),
        new(">"),
        new(@"\]\]>"),
    ];

    /// <summary>Parses a document: its blocks, then the inlines in them.</summary>
    public static MdNode Parse(string markdown)
    {
        var parser = new MdBlocks();
        parser.Run(markdown);
        new MdInlines(parser._refs).Process(parser._doc);
        return parser._doc;
    }

    private void Run(string input)
    {
        _tip = _doc;
        _oldTip = _doc;
        _lastMatched = _doc;
        input = input.Replace('\0', (char)0xFFFD);
        _cellBudget = Math.Max(10_000, input.Length);
        var lines = Lines(input);
        foreach (var line in lines) IncorporateLine(line);
        while (_tip is not null) Finalize(_tip, lines.Count);
    }

    private static List<string> Lines(string input)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] is not ('\n' or '\r')) continue;
            lines.Add(input[start..i]);
            if (input[i] == '\r' && i + 1 < input.Length && input[i + 1] == '\n') i++;
            start = i + 1;
        }

        if (start < input.Length) lines.Add(input[start..]);
        return lines;
    }

    private int Peek(int position) => position < _line.Length ? _line[position] : -1;

    private static bool IsSpaceOrTab(int c) => c is ' ' or '\t';

    private void IncorporateLine(string line)
    {
        var container = _doc;
        _oldTip = _tip!;
        _offset = 0;
        _column = 0;
        _blank = false;
        _partiallyConsumedTab = false;
        _lineNumber++;
        _line = line;

        // Which open blocks this line continues: each one's own rule, from the outside in.
        while (container.LastChild is { Open: true } lastChild)
        {
            container = lastChild;
            FindNextNonspace();
            var result = Continue(container);
            if (result == 2) return;
            if (result == 1)
            {
                container = container.Parent!;
                break;
            }
        }

        _allClosed = container == _oldTip;
        _lastMatched = container;

        var matchedLeaf = container.Type is not (MdType.Paragraph or MdType.Table) && AcceptsLines(container.Type);
        while (!matchedLeaf)
        {
            FindNextNonspace();
            // Most lines start no block: a quick check before trying each kind.
            if (!_indented && !MaybeSpecial(_nextNonspace))
            {
                AdvanceNextNonspace();
                break;
            }

            var result = 0;
            foreach (var start in Starts)
            {
                result = start(this, container);
                if (result != 0) break;
            }

            if (result == 1)
            {
                container = _tip!;
            }
            else if (result == 2)
            {
                container = _tip!;
                matchedLeaf = true;
            }
            else
            {
                AdvanceNextNonspace();
                break;
            }
        }

        // What remains is text: a lazy continuation of a paragraph, or added to the container.
        if (!_allClosed && !_blank && _tip!.Type == MdType.Paragraph)
        {
            AddLine();
        }
        else
        {
            CloseUnmatchedBlocks();
            var type = container.Type;
            if (AcceptsLines(type))
            {
                AddLine();
                if (type == MdType.HtmlBlock && container.HtmlBlockType is >= 1 and <= 5 && HtmlBlockClose[container.HtmlBlockType].IsMatch(_line[_offset..]))
                {
                    _lastLineLength = line.Length;
                    Finalize(container, _lineNumber);
                }
            }
            else if (_offset < line.Length && !_blank)
            {
                AddChild(MdType.Paragraph);
                AdvanceNextNonspace();
                AddLine();
            }
        }

        _lastLineLength = line.Length;
    }

    private bool MaybeSpecial(int position)
    {
        var c = Peek(position);
        return c is '#' or '`' or '~' or '*' or '+' or '_' or '=' or '<' or '>' or '-' or '|' or ':' or (>= '0' and <= '9');
    }

    // 0: matched, 1: not matched, 2: the line is used up (a closing fence).
    private int Continue(MdNode block)
    {
        switch (block.Type)
        {
            case MdType.Document or MdType.List:
                return 0;
            case MdType.BlockQuote:
                if (!_indented && Peek(_nextNonspace) == '>')
                {
                    AdvanceNextNonspace();
                    AdvanceOffset(1, false);
                    if (IsSpaceOrTab(Peek(_offset))) AdvanceOffset(1, true);
                    return 0;
                }

                return 1;
            case MdType.Item:
                if (_blank)
                {
                    if (block.FirstChild is null) return 1;
                    AdvanceNextNonspace();
                }
                else if (_indent >= block.List!.MarkerOffset + block.List.Padding)
                {
                    AdvanceOffset(block.List.MarkerOffset + block.List.Padding, true);
                }
                else
                {
                    return 1;
                }

                return 0;
            case MdType.Heading or MdType.ThematicBreak:
                return 1;
            case MdType.CodeBlock:
                if (block.Fenced)
                {
                    var match = _indent <= 3 && Peek(_nextNonspace) == block.FenceChar ? ClosingCodeFence().Match(_line[_nextNonspace..]) : null;
                    if (match is { Success: true } && match.Length >= block.FenceLength)
                    {
                        _lastLineLength = _offset + _indent + match.Length;
                        Finalize(block, _lineNumber);
                        return 2;
                    }

                    // The fence's own indent comes off each line.
                    for (var i = block.FenceOffset; i > 0 && IsSpaceOrTab(Peek(_offset)); i--) AdvanceOffset(1, true);
                }
                else if (_indent >= CodeIndent)
                {
                    AdvanceOffset(CodeIndent, true);
                }
                else if (_blank)
                {
                    AdvanceNextNonspace();
                }
                else
                {
                    return 1;
                }

                return 0;
            case MdType.HtmlBlock:
                return _blank && block.HtmlBlockType is 6 or 7 ? 1 : 0;
            case MdType.Paragraph:
                return _blank ? 1 : 0;
            case MdType.Table:
                if (_blank) return 1;
                // Short rows are filled with empty cells, up to a budget the size of the input:
                // past it the table ends, so a few bytes can't make megabytes of cells.
                var missing = block.Align!.Count - SplitRow(_line[_nextNonspace..]).Count;
                if (missing > 0 && (_filledCells += missing) > _cellBudget) return 1;
                return 0;
            default:
                return 0;
        }
    }

    private static bool AcceptsLines(MdType type) => type is MdType.Paragraph or MdType.CodeBlock or MdType.HtmlBlock or MdType.Table;

    private static bool CanContain(MdType parent, MdType child) => parent switch
    {
        MdType.Document or MdType.BlockQuote or MdType.Item => child != MdType.Item,
        MdType.List => child == MdType.Item,
        _ => false,
    };

    private static readonly Func<MdBlocks, MdNode, int>[] Starts =
    [
        (p, _) => p.BlockQuoteStart(),
        (p, _) => p.AtxHeadingStart(),
        (p, _) => p.FencedCodeStart(),
        (p, c) => p.HtmlBlockStart(c),
        (p, c) => p.SetextHeadingStart(c),
        (p, c) => p.TableStart(c),
        (p, _) => p.ThematicBreakStart(),
        (p, c) => p.ListItemStart(c),
        (p, _) => p.IndentedCodeStart(),
    ];

    private int BlockQuoteStart()
    {
        if (_indented || Peek(_nextNonspace) != '>' || TooDeep()) return 0;
        AdvanceNextNonspace();
        AdvanceOffset(1, false);
        if (IsSpaceOrTab(Peek(_offset))) AdvanceOffset(1, true);
        CloseUnmatchedBlocks();
        AddChild(MdType.BlockQuote);
        return 1;
    }

    private int AtxHeadingStart()
    {
        if (_indented) return 0;
        var match = AtxHeadingMarker().Match(_line[_nextNonspace..]);
        if (!match.Success) return 0;
        AdvanceNextNonspace();
        AdvanceOffset(match.Length, false);
        CloseUnmatchedBlocks();
        var heading = AddChild(MdType.Heading);
        heading.Level = match.Value.Trim().Length;
        var text = HeadingClosingHashes().Replace(HeadingOnlyHashes().Replace(_line[_offset..], ""), "");
        heading.Content.Append(text);
        AdvanceOffset(_line.Length - _offset, false);
        return 2;
    }

    private int FencedCodeStart()
    {
        if (_indented) return 0;
        var match = CodeFence().Match(_line[_nextNonspace..]);
        if (!match.Success) return 0;
        CloseUnmatchedBlocks();
        var code = AddChild(MdType.CodeBlock);
        code.Fenced = true;
        code.FenceLength = match.Length;
        code.FenceChar = match.Value[0];
        code.FenceOffset = _indent;
        AdvanceNextNonspace();
        AdvanceOffset(match.Length, false);
        return 2;
    }

    private int HtmlBlockStart(MdNode container)
    {
        if (_indented || Peek(_nextNonspace) != '<') return 0;
        var rest = _line[_nextNonspace..];
        for (var type = 1; type <= 7; type++)
        {
            // A type 7 block can't interrupt a paragraph, even a lazy one.
            if (!HtmlBlockOpen[type].IsMatch(rest)) continue;
            if (type == 7 && (container.Type == MdType.Paragraph || (!_allClosed && !_blank && _tip!.Type == MdType.Paragraph))) continue;
            CloseUnmatchedBlocks();
            var block = AddChild(MdType.HtmlBlock);
            block.HtmlBlockType = type;
            return 2;
        }

        return 0;
    }

    private int SetextHeadingStart(MdNode container)
    {
        if (_indented || container.Type != MdType.Paragraph || !SetextHeadingLine().IsMatch(_line[_nextNonspace..])) return 0;
        CloseUnmatchedBlocks();
        // Link reference definitions at its start are definitions, not the heading's text.
        var content = ResolveReferences(container.Content.ToString());
        container.Content = new StringBuilder(content);
        if (content.Length == 0) return 0;
        var heading = new MdNode(MdType.Heading) { StartLine = container.StartLine, Level = Peek(_nextNonspace) == '=' ? 1 : 2 };
        heading.Content.Append(content);
        container.InsertAfter(heading);
        container.Unlink();
        _tip = heading;
        AdvanceOffset(_line.Length - _offset, false);
        return 2;
    }

    // A GitHub-flavored table: a paragraph's last line as its header, then a delimiter row with
    // as many cells (|---|:--:|), then rows until a blank line or another block.
    private int TableStart(MdNode container)
    {
        if (_indented || container.Type != MdType.Paragraph) return 0;
        var delimiter = _line[_nextNonspace..];
        if (ParseDelimiterRow(delimiter) is not { } align) return 0;
        // The paragraph's last line is the header (the whole text isn't copied for every candidate).
        var lineStart = Math.Min(container.LastLineStart, container.Content.Length);
        var header = container.Content.ToString(lineStart, container.Content.Length - lineStart).TrimEnd('\n');
        if (!header.Contains('|') && !delimiter.Contains('|')) return 0;
        var cells = SplitRow(header);
        if (cells.Count != align.Count) return 0;

        CloseUnmatchedBlocks();
        var table = new MdNode(MdType.Table) { StartLine = _lineNumber - 1, Align = align };
        container.InsertAfter(table);
        if (lineStart == 0)
        {
            container.Unlink();
        }
        else
        {
            container.Content.Length = lineStart;
            _tip = container;
            Finalize(container, _lineNumber - 2);
        }

        table.Append(Row(cells, align.Count, head: true));
        _tip = table;
        AdvanceOffset(_line.Length - _offset, false);
        return 2;
    }

    private static List<string?>? ParseDelimiterRow(string line)
    {
        var text = line.Trim();
        if (text.Length == 0 || !text.Contains('-')) return null;
        if (text.StartsWith('|')) text = text[1..];
        if (text.EndsWith('|')) text = text[..^1];
        var align = new List<string?>();
        var parts = text.Split('|');
        // A table this wide is no table; wider would let a short input fill huge rows.
        if (parts.Length > MaxColumns) return null;
        foreach (var part in parts)
        {
            var cell = part.Trim();
            if (!DelimiterCell().IsMatch(cell)) return null;
            var left = cell[0] == ':';
            var right = cell[^1] == ':';
            align.Add(left && right ? "center" : right ? "right" : left ? "left" : null);
        }

        return align;
    }

    // A row's cells: split at pipes that aren't escaped; an escaped pipe is a pipe in its cell.
    private static List<string> SplitRow(string line)
    {
        var text = line.Trim();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var start = text.StartsWith('|') ? 1 : 0;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < text.Length)
            {
                if (text[i + 1] != '|') cell.Append('\\');
                cell.Append(text[++i]);
            }
            else if (c == '|')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else
            {
                cell.Append(c);
            }
        }

        // A last pipe ends the row; anything after the last pipe is a cell.
        if (cell.Length > 0 || !text.EndsWith('|') || text.Length == start) cells.Add(cell.ToString().Trim());
        return cells;
    }

    private static MdNode Row(List<string> cells, int columns, bool head)
    {
        var row = new MdNode(MdType.TableRow) { Head = head, Open = false };
        for (var i = 0; i < columns; i++)
        {
            var cell = new MdNode(MdType.TableCell) { Head = head, Open = false, Level = i };
            cell.Content.Append(i < cells.Count ? cells[i] : "");
            row.Append(cell);
        }

        return row;
    }

    private int ThematicBreakStart()
    {
        if (_indented || !ThematicBreak().IsMatch(_line[_nextNonspace..])) return 0;
        CloseUnmatchedBlocks();
        AddChild(MdType.ThematicBreak);
        AdvanceOffset(_line.Length - _offset, false);
        return 2;
    }

    private int ListItemStart(MdNode container)
    {
        if (_indented && container.Type != MdType.List || TooDeep()) return 0;
        if (ParseListMarker(container) is not { } data) return 0;
        CloseUnmatchedBlocks();
        if (_tip!.Type != MdType.List || !ListsMatch(_tip.List!, data))
        {
            var list = AddChild(MdType.List);
            list.List = data;
        }

        var item = AddChild(MdType.Item);
        item.List = data;
        return 1;
    }

    // Quotes and lists nest at most this deep; deeper markers are text. Each line walks the open
    // blocks, so thousands deep would take quadratic time (and the sanitizer keeps 64 levels).
    private bool TooDeep()
    {
        var depth = 0;
        for (var block = _tip; block is not null; block = block.Parent) depth++;
        return depth > 100;
    }

    private static bool ListsMatch(MdList a, MdList b) => a.Ordered == b.Ordered && a.Delimiter == b.Delimiter && a.Bullet == b.Bullet;

    private MdList? ParseListMarker(MdNode container)
    {
        if (_indent >= 4) return null;
        var rest = _line[_nextNonspace..];
        MdList data;
        int markerLength;
        if (rest.Length > 0 && rest[0] is '*' or '+' or '-')
        {
            data = new MdList { Bullet = rest[0], MarkerOffset = _indent };
            markerLength = 1;
        }
        else if (OrderedListMarker().Match(rest) is { Success: true } match && (container.Type != MdType.Paragraph || match.Groups[1].Value == "1"))
        {
            data = new MdList { Ordered = true, Start = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), Delimiter = match.Groups[2].Value[0], MarkerOffset = _indent };
            markerLength = match.Length;
        }
        else
        {
            return null;
        }

        // A space or tab after the marker, or the line's end.
        var next = Peek(_nextNonspace + markerLength);
        if (!(next == -1 || IsSpaceOrTab(next))) return null;
        // An item can interrupt a paragraph only if it isn't empty.
        if (container.Type == MdType.Paragraph && _line[(_nextNonspace + markerLength)..].Trim(' ', '\t').Length == 0) return null;

        AdvanceNextNonspace();
        AdvanceOffset(markerLength, true);
        var spacesStartColumn = _column;
        var spacesStartOffset = _offset;
        do
        {
            AdvanceOffset(1, true);
            next = Peek(_offset);
        }
        while (_column - spacesStartColumn < 5 && IsSpaceOrTab(next));

        var blankItem = Peek(_offset) == -1;
        var spacesAfterMarker = _column - spacesStartColumn;
        if (spacesAfterMarker >= 5 || spacesAfterMarker < 1 || blankItem)
        {
            data.Padding = markerLength + 1;
            _column = spacesStartColumn;
            _offset = spacesStartOffset;
            if (IsSpaceOrTab(Peek(_offset))) AdvanceOffset(1, true);
        }
        else
        {
            data.Padding = markerLength + spacesAfterMarker;
        }

        return data;
    }

    private int IndentedCodeStart()
    {
        if (!_indented || _tip!.Type == MdType.Paragraph || _blank) return 0;
        AdvanceOffset(CodeIndent, true);
        CloseUnmatchedBlocks();
        AddChild(MdType.CodeBlock);
        return 2;
    }

    private void AdvanceOffset(int count, bool columns)
    {
        while (count > 0 && _offset < _line.Length)
        {
            var c = _line[_offset];
            if (c == '\t')
            {
                var charsToTab = 4 - (_column % 4);
                if (columns)
                {
                    _partiallyConsumedTab = charsToTab > count;
                    var charsToAdvance = charsToTab > count ? count : charsToTab;
                    _column += charsToAdvance;
                    _offset += _partiallyConsumedTab ? 0 : 1;
                    count -= charsToAdvance;
                }
                else
                {
                    _partiallyConsumedTab = false;
                    _column += charsToTab;
                    _offset++;
                    count--;
                }
            }
            else
            {
                _partiallyConsumedTab = false;
                _offset++;
                _column++;
                count--;
            }
        }
    }

    private void AdvanceNextNonspace()
    {
        _offset = _nextNonspace;
        _column = _nextNonspaceColumn;
        _partiallyConsumedTab = false;
    }

    private void FindNextNonspace()
    {
        var i = _offset;
        var columns = _column;
        while (i < _line.Length)
        {
            var c = _line[i];
            if (c == ' ')
            {
                i++;
                columns++;
            }
            else if (c == '\t')
            {
                i++;
                columns += 4 - (columns % 4);
            }
            else
            {
                break;
            }
        }

        _blank = i >= _line.Length;
        _nextNonspace = i;
        _nextNonspaceColumn = columns;
        _indent = _nextNonspaceColumn - _column;
        _indented = _indent >= CodeIndent;
    }

    private void AddLine()
    {
        var tip = _tip!;
        if (tip.Type == MdType.Table)
        {
            // A body row; the delimiter row leaves nothing.
            var rest = _line[_offset..];
            if (rest.Trim().Length > 0) tip.Append(Row(SplitRow(rest), tip.Align!.Count, head: false));
            return;
        }

        tip.LastLineStart = tip.Content.Length;
        if (_partiallyConsumedTab)
        {
            _offset++;
            tip.Content.Append(' ', 4 - (_column % 4));
        }

        tip.Content.Append(_line, _offset, _line.Length - _offset).Append('\n');
    }

    private MdNode AddChild(MdType type)
    {
        while (!CanContain(_tip!.Type, type)) Finalize(_tip, _lineNumber - 1);
        var block = new MdNode(type) { StartLine = _lineNumber };
        _tip.Append(block);
        _tip = block;
        return block;
    }

    private void CloseUnmatchedBlocks()
    {
        if (_allClosed) return;
        while (_oldTip != _lastMatched)
        {
            var parent = _oldTip.Parent!;
            Finalize(_oldTip, _lineNumber - 1);
            _oldTip = parent;
        }

        _allClosed = true;
    }

    private void Finalize(MdNode block, int lineNumber)
    {
        var above = block.Parent;
        block.Open = false;
        block.EndLine = lineNumber;
        switch (block.Type)
        {
            case MdType.Paragraph:
                // A paragraph that was only link reference definitions goes.
                var content = ResolveReferences(block.Content.ToString());
                block.Content = new StringBuilder(content);
                if (content.Trim().Length == 0) block.Unlink();
                break;
            case MdType.CodeBlock:
                FinalizeCode(block);
                break;
            case MdType.HtmlBlock:
                block.Literal = TrimTrailingBlankLines(block.Content.ToString());
                break;
            case MdType.Item:
                block.EndLine = block.LastChild?.EndLine ?? block.StartLine;
                break;
            case MdType.List:
                FinalizeList(block);
                break;
        }

        _tip = above;
    }

    // Line breaks, each followed by spaces only, off the end (a regex for it is quadratic).
    private static string TrimTrailingBlankLines(string text)
    {
        var end = text.Length;
        while (true)
        {
            var i = end;
            while (i > 0 && text[i - 1] == ' ') i--;
            if (i == 0 || text[i - 1] != '\n') break;
            end = i - 1;
        }

        return text[..end];
    }

    // Definitions at a paragraph's start ([label]: destination "title") go into the map; what
    // follows them is the paragraph's text.
    private string ResolveReferences(string content)
    {
        var inlines = new MdInlines(_refs);
        var at = 0;
        while (at < content.Length && content[at] == '[')
        {
            var used = inlines.ParseReference(content, at);
            if (used == 0) break;
            at += used;
        }

        return at == 0 ? content : content[at..];
    }

    private static void FinalizeCode(MdNode block)
    {
        var content = block.Content.ToString();
        if (block.Fenced)
        {
            var newline = content.IndexOf('\n');
            var firstLine = newline < 0 ? content : content[..newline];
            block.Info = MdSyntax.Unescape(firstLine.Trim());
            block.Literal = newline < 0 ? "" : content[(newline + 1)..];
        }
        else
        {
            var lines = content.Split('\n').ToList();
            while (lines.Count > 0 && lines[^1].Trim(' ', '\t').Length == 0) lines.RemoveAt(lines.Count - 1);
            block.Literal = string.Join('\n', lines) + "\n";
            block.EndLine = block.StartLine + lines.Count - 1;
        }
    }

    // A list is loose when blank lines separate its items, or blocks inside an item.
    private static void FinalizeList(MdNode list)
    {
        for (var item = list.FirstChild; item is not null; item = item.Next)
        {
            if (item.Next is not null && EndsWithBlankLine(item))
            {
                list.List!.Tight = false;
                break;
            }

            var loose = false;
            for (var sub = item.FirstChild; sub is not null; sub = sub.Next)
            {
                if (sub.Next is not null && EndsWithBlankLine(sub))
                {
                    loose = true;
                    break;
                }
            }

            if (loose)
            {
                list.List!.Tight = false;
                break;
            }
        }

        list.EndLine = list.LastChild?.EndLine ?? list.StartLine;
    }

    private static bool EndsWithBlankLine(MdNode block) => block.Next is not null && block.EndLine != block.Next.StartLine - 1;

    /// <summary>Whether a paragraph's text starts with a task list marker ([ ] or [x], then white space); removes it.</summary>
    internal static bool TakeTaskMarker(MdNode paragraph, out bool done)
    {
        done = false;
        var content = paragraph.Content.ToString();
        var match = TaskMarker().Match(content);
        if (!match.Success) return false;
        done = match.Groups[1].Value is "x" or "X";
        paragraph.Content = new StringBuilder(content[match.Length..]);
        return true;
    }
}
