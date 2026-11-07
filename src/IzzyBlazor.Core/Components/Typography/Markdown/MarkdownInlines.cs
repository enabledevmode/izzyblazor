using System.Text.RegularExpressions;

namespace IzzyBlazor;

/// <summary>
/// Markdown's inlines (CommonMark 0.31.2, with GitHub-flavored strikethrough and extended
/// autolinks), parsed in each paragraph, heading and table cell: the reference parser's
/// algorithm, with its delimiter stack for emphasis and its bracket stack for links.
/// </summary>
internal sealed partial class MdInlines(Dictionary<string, (string Destination, string Title)> refs)
{
    private string _subject = "";
    private int _pos;
    private Delimiter? _delimiters;
    private Bracket? _brackets;
    private (int Gt, int Comment, int Pi, int Cdata) _lastClose;

    private sealed class Delimiter
    {
        public char Char;
        public int Count;
        public int Original;
        public MdNode Node = null!;
        public Delimiter? Previous;
        public Delimiter? Next;
        public bool CanOpen;
        public bool CanClose;
    }

    private sealed class Bracket
    {
        public MdNode Node = null!;
        public Bracket? Previous;
        public Delimiter? PreviousDelimiter;
        public int Index;
        public bool Image;
        public bool Active = true;
        public bool BracketAfter;
    }

    [GeneratedRegex(@"\G(?:""(?:\\[\s\S]|[^\\""\x00])*""|'(?:\\[\s\S]|[^\\'\x00])*'|\((?:\\[\s\S]|[^\\()\x00])*\))")]
    private static partial Regex LinkTitle();

    [GeneratedRegex(@"\G<(?:[^<>\n\\\x00]|\\.)*>")]
    private static partial Regex LinkDestinationBraces();

    [GeneratedRegex(@"\G" + MdSyntax.Entity, RegexOptions.IgnoreCase)]
    private static partial Regex EntityHere();

    [GeneratedRegex(@"\G<([a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*)>")]
    private static partial Regex EmailAutolink();

    [GeneratedRegex(@"\G<[A-Za-z][A-Za-z0-9.+-]{1,31}:[^<>\x00-\x20]*>")]
    private static partial Regex Autolink();

    [GeneratedRegex(@"\G" + MdSyntax.HtmlTag, RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTagHere();

    [GeneratedRegex(@"\G\[(?:[^\\\[\]]|\\[\s\S]){0,1000}\]")]
    private static partial Regex LinkLabel();

    [GeneratedRegex(@"\G *(?:\n *)?")]
    private static partial Regex SpaceNewlineSpace();

    [GeneratedRegex(@"\G *(?:\n|$)")]
    private static partial Regex SpaceAtEndOfLine();

    /// <summary>Parses the inlines of every paragraph, heading and table cell in a document.</summary>
    public void Process(MdNode document)
    {
        var blocks = new List<MdNode>();
        Collect(document, blocks);
        foreach (var block in blocks)
        {
            // A task list item: its first paragraph starts with [ ] or [x].
            if (block.Type == MdType.Paragraph && block.Parent is { Type: MdType.Item } item && item.FirstChild == block
                && MdBlocks.TakeTaskMarker(block, out var done))
            {
                Parse(block, block.Content.ToString());
                block.Prepend(new MdNode(MdType.TaskBox) { Checked = done });
                continue;
            }

            Parse(block, block.Content.ToString());
        }
    }

    // Without recursion: block quotes and lists can nest thousands deep.
    private static void Collect(MdNode root, List<MdNode> blocks)
    {
        var pending = new Stack<MdNode>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            for (var child = node.LastChild; child is not null; child = child.Prev)
            {
                if (child.Type is MdType.Paragraph or MdType.Heading or MdType.TableCell) blocks.Add(child);
                else pending.Push(child);
            }
        }

        blocks.Reverse();
    }

    private void Parse(MdNode block, string content)
    {
        _subject = content.Trim();
        _pos = 0;
        _delimiters = null;
        _brackets = null;
        _lastClose = (_subject.LastIndexOf('>'), _subject.LastIndexOf("-->", StringComparison.Ordinal), _subject.LastIndexOf("?>", StringComparison.Ordinal), _subject.LastIndexOf("]]>", StringComparison.Ordinal));
        while (ParseInline(block)) { }
        block.Content.Clear();
        ProcessEmphasis(null);
        ExtendedAutolinks(block);
    }

    private int Peek() => _pos < _subject.Length ? _subject[_pos] : -1;

    private string? Match(Regex regex)
    {
        var match = regex.Match(_subject, _pos);
        if (!match.Success) return null;
        _pos = match.Index + match.Length;
        return match.Value;
    }

    private bool ParseInline(MdNode block)
    {
        var c = Peek();
        if (c == -1) return false;
        var handled = c switch
        {
            '\n' => ParseNewline(block),
            '\\' => ParseBackslash(block),
            '`' => ParseBackticks(block),
            '*' or '_' or '~' => HandleDelimiter((char)c, block),
            '[' => ParseOpenBracket(block),
            '!' => ParseBang(block),
            ']' => ParseCloseBracket(block),
            '<' => ParseAutolink(block) || ParseHtmlTag(block),
            '&' => ParseEntity(block),
            _ => ParseString(block),
        };
        if (!handled)
        {
            _pos++;
            block.Append(MdNode.Text(((char)c).ToString()));
        }

        return true;
    }

    private static bool IsSpecial(char c) => c is '\n' or '`' or '[' or ']' or '\\' or '!' or '<' or '&' or '*' or '_' or '~';

    private bool ParseString(MdNode block)
    {
        var start = _pos;
        while (_pos < _subject.Length && !IsSpecial(_subject[_pos])) _pos++;
        if (_pos == start) return false;
        block.Append(MdNode.Text(_subject[start.._pos]));
        return true;
    }

    private bool ParseNewline(MdNode block)
    {
        _pos++;
        // Two spaces or more before a line break make a hard break; one or none, a soft one.
        if (block.LastChild is { Type: MdType.Text } last && last.Literal.EndsWith(' '))
        {
            var hard = last.Literal.Length >= 2 && last.Literal[^2] == ' ';
            last.Literal = last.Literal.TrimEnd(' ');
            block.Append(new MdNode(hard ? MdType.LineBreak : MdType.SoftBreak));
        }
        else
        {
            block.Append(new MdNode(MdType.SoftBreak));
        }

        while (_pos < _subject.Length && _subject[_pos] == ' ') _pos++;
        return true;
    }

    private bool ParseBackslash(MdNode block)
    {
        _pos++;
        if (Peek() == '\n')
        {
            _pos++;
            block.Append(new MdNode(MdType.LineBreak));
        }
        else if (Peek() != -1 && MdSyntax.IsAsciiPunctuation(Peek()))
        {
            block.Append(MdNode.Text(_subject[_pos].ToString()));
            _pos++;
        }
        else
        {
            block.Append(MdNode.Text("\\"));
        }

        return true;
    }

    private bool ParseBackticks(MdNode block)
    {
        var start = _pos;
        while (Peek() == '`') _pos++;
        var length = _pos - start;
        var afterOpen = _pos;
        while (true)
        {
            var next = _subject.IndexOf('`', _pos);
            if (next < 0) break;
            var end = next;
            while (end < _subject.Length && _subject[end] == '`') end++;
            _pos = end;
            if (end - next != length) continue;
            var contents = _subject[afterOpen..next].Replace('\n', ' ');
            if (contents.Length > 1 && contents[0] == ' ' && contents[^1] == ' ' && contents.Any(ch => ch != ' ')) contents = contents[1..^1];
            block.Append(new MdNode(MdType.Code) { Literal = contents });
            return true;
        }

        // No closing run of the same length: the backticks are text.
        _pos = afterOpen;
        block.Append(MdNode.Text(new string('`', length)));
        return true;
    }

    private (int Count, bool CanOpen, bool CanClose) ScanDelimiters(char c)
    {
        var start = _pos;
        var count = 0;
        while (Peek() == c)
        {
            count++;
            _pos++;
        }

        var before = start == 0 ? '\n' : _subject[start - 1];
        var after = Peek() == -1 ? '\n' : (char)Peek();
        _pos = start;

        var afterWhitespace = MdSyntax.IsUnicodeWhitespace(after);
        var afterPunctuation = MdSyntax.IsPunctuation(after);
        var beforeWhitespace = MdSyntax.IsUnicodeWhitespace(before);
        var beforePunctuation = MdSyntax.IsPunctuation(before);
        var leftFlanking = !afterWhitespace && (!afterPunctuation || beforeWhitespace || beforePunctuation);
        var rightFlanking = !beforeWhitespace && (!beforePunctuation || afterWhitespace || afterPunctuation);
        return c == '_'
            ? (count, leftFlanking && (!rightFlanking || beforePunctuation), rightFlanking && (!leftFlanking || afterPunctuation))
            : (count, leftFlanking, rightFlanking);
    }

    private bool HandleDelimiter(char c, MdNode block)
    {
        var (count, canOpen, canClose) = ScanDelimiters(c);
        var start = _pos;
        _pos += count;
        var node = MdNode.Text(_subject[start.._pos]);
        block.Append(node);
        // Strikethrough is one or two tildes; three or more are text.
        if (c == '~' && count > 2) return true;
        if (!canOpen && !canClose) return true;
        _delimiters = new Delimiter
        {
            Char = c, Count = count, Original = count, Node = node, Previous = _delimiters, CanOpen = canOpen, CanClose = canClose,
        };
        if (_delimiters.Previous is not null) _delimiters.Previous.Next = _delimiters;
        return true;
    }

    private void RemoveDelimiter(Delimiter delimiter)
    {
        if (delimiter.Previous is not null) delimiter.Previous.Next = delimiter.Next;
        if (delimiter.Next is null) _delimiters = delimiter.Previous;
        else delimiter.Next.Previous = delimiter.Previous;
    }

    private static void RemoveDelimitersBetween(Delimiter bottom, Delimiter top)
    {
        if (bottom.Next == top) return;
        bottom.Next = top;
        top.Previous = bottom;
    }

    private void ProcessEmphasis(Delimiter? stackBottom)
    {
        var openersBottom = new Delimiter?[16];
        Array.Fill(openersBottom, stackBottom);

        var closer = _delimiters;
        while (closer is not null && closer.Previous != stackBottom) closer = closer.Previous;

        while (closer is not null)
        {
            if (!closer.CanClose)
            {
                closer = closer.Next;
                continue;
            }

            var index = closer.Char switch
            {
                '_' => 2 + (closer.CanOpen ? 3 : 0) + closer.Original % 3,
                '*' => 8 + (closer.CanOpen ? 3 : 0) + closer.Original % 3,
                _ => 14 + (closer.Original == 2 ? 1 : 0),
            };
            var opener = closer.Previous;
            var found = false;
            while (opener is not null && opener != stackBottom && opener != openersBottom[index])
            {
                if (closer.Char == '~')
                {
                    // The nearest tilde opener, as GitHub's: a different count means no strikethrough.
                    if (opener.Char == '~' && opener.CanOpen)
                    {
                        found = opener.Original == closer.Original;
                        break;
                    }
                }
                else
                {
                    var oddMatch = (closer.CanOpen || opener.CanClose) && closer.Original % 3 != 0 && (opener.Original + closer.Original) % 3 == 0;
                    if (opener.Char == closer.Char && opener.CanOpen && !oddMatch)
                    {
                        found = true;
                        break;
                    }
                }

                opener = opener.Previous;
            }

            var oldCloser = closer;
            if (!found)
            {
                closer = closer.Next;
                openersBottom[index] = oldCloser.Previous;
                if (!oldCloser.CanOpen) RemoveDelimiter(oldCloser);
                continue;
            }

            var use = closer.Char == '~' ? closer.Count : closer.Count >= 2 && opener!.Count >= 2 ? 2 : 1;
            var openerNode = opener!.Node;
            var closerNode = closer.Node;
            opener.Count -= use;
            closer.Count -= use;
            openerNode.Literal = openerNode.Literal[..^use];
            closerNode.Literal = closerNode.Literal[..^use];

            var wrapper = new MdNode(closer.Char == '~' ? MdType.Strike : use == 1 ? MdType.Emph : MdType.Strong);
            var item = openerNode.Next;
            while (item is not null && item != closerNode)
            {
                var next = item.Next;
                wrapper.Append(item);
                item = next;
            }

            openerNode.InsertAfter(wrapper);
            RemoveDelimitersBetween(opener, closer);
            if (opener.Count == 0)
            {
                openerNode.Unlink();
                RemoveDelimiter(opener);
            }

            if (closer.Count == 0)
            {
                closerNode.Unlink();
                var next = closer.Next;
                RemoveDelimiter(closer);
                closer = next;
            }
        }

        while (_delimiters is not null && _delimiters != stackBottom) RemoveDelimiter(_delimiters);
    }

    private bool ParseOpenBracket(MdNode block)
    {
        var start = _pos;
        _pos++;
        var node = MdNode.Text("[");
        block.Append(node);
        AddBracket(node, start, image: false);
        return true;
    }

    private bool ParseBang(MdNode block)
    {
        var start = _pos;
        _pos++;
        if (Peek() == '[')
        {
            _pos++;
            var node = MdNode.Text("![");
            block.Append(node);
            AddBracket(node, start + 1, image: true);
        }
        else
        {
            block.Append(MdNode.Text("!"));
        }

        return true;
    }

    private void AddBracket(MdNode node, int index, bool image)
    {
        if (_brackets is not null) _brackets.BracketAfter = true;
        _brackets = new Bracket { Node = node, Previous = _brackets, PreviousDelimiter = _delimiters, Index = index, Image = image };
    }

    private bool ParseCloseBracket(MdNode block)
    {
        _pos++;
        var start = _pos;
        var opener = _brackets;
        if (opener is null)
        {
            block.Append(MdNode.Text("]"));
            return true;
        }

        if (!opener.Active)
        {
            block.Append(MdNode.Text("]"));
            _brackets = opener.Previous;
            return true;
        }

        var image = opener.Image;
        var save = _pos;
        string? destination = null;
        string? title = null;
        var matched = false;

        // An inline link: [text](destination "title").
        if (Peek() == '(')
        {
            _pos++;
            SpaceNewline();
            if ((destination = ParseLinkDestination()) is not null)
            {
                SpaceNewline();
                if (_pos > 0 && IsWhitespaceChar(_subject[_pos - 1])) title = ParseLinkTitle();
                SpaceNewline();
                if (Peek() == ')')
                {
                    _pos++;
                    matched = true;
                }
            }

            if (!matched) _pos = save;
        }

        // A reference link: [text][label], [text][] or [text].
        if (!matched)
        {
            var beforeLabel = _pos;
            var n = ParseLinkLabel();
            string? label = null;
            if (n > 2) label = _subject.Substring(beforeLabel, n);
            else if (!opener.BracketAfter) label = _subject[opener.Index..start];
            if (n == 0) _pos = save;
            if (label is not null && refs.TryGetValue(MdSyntax.NormalizeReference(label), out var link))
            {
                destination = link.Destination;
                title = link.Title;
                matched = true;
            }
        }

        if (!matched)
        {
            _brackets = opener.Previous;
            _pos = start;
            block.Append(MdNode.Text("]"));
            return true;
        }

        var node = new MdNode(image ? MdType.Image : MdType.Link) { Destination = destination!, Title = title ?? "" };
        var item = opener.Node.Next;
        while (item is not null)
        {
            var next = item.Next;
            node.Append(item);
            item = next;
        }

        block.Append(node);
        ProcessEmphasis(opener.PreviousDelimiter);
        _brackets = opener.Previous;
        opener.Node.Unlink();
        // No links in links: earlier link openers can't make links any more.
        if (!image)
        {
            for (var earlier = _brackets; earlier is not null; earlier = earlier.Previous)
            {
                if (earlier.Image) continue;
                // Below an inactive link opener, every one is inactive already.
                if (!earlier.Active) break;
                earlier.Active = false;
            }
        }

        return true;
    }

    private static bool IsWhitespaceChar(char c) => c is ' ' or '\t' or '\n' or '\x0b' or '\x0c' or '\r';

    private void SpaceNewline() => Match(SpaceNewlineSpace());

    private string? ParseLinkDestination()
    {
        if (Match(LinkDestinationBraces()) is { } braced) return MdSyntax.NormalizeUri(MdSyntax.Unescape(braced[1..^1]));
        if (Peek() == '<') return null;

        var save = _pos;
        var parens = 0;
        var c = -1;
        while ((c = Peek()) != -1)
        {
            if (c == '\\' && _pos + 1 < _subject.Length && MdSyntax.IsAsciiPunctuation(_subject[_pos + 1]))
            {
                _pos += 2;
            }
            else if (c == '(')
            {
                _pos++;
                parens++;
                // CommonMark allows a limit on nesting; past it, scanning on would make some
                // text take quadratic time.
                if (parens > 32) return null;
            }
            else if (c == ')')
            {
                if (parens < 1) break;
                _pos++;
                parens--;
            }
            else if (c <= 0x20 || c == 0x7f)
            {
                break;
            }
            else
            {
                _pos++;
            }
        }

        if (_pos == save && c != ')') return null;
        if (parens != 0) return null;
        return MdSyntax.NormalizeUri(MdSyntax.Unescape(_subject[save.._pos]));
    }

    private string? ParseLinkTitle() => Match(LinkTitle()) is { } title ? MdSyntax.Unescape(title[1..^1]) : null;

    private int ParseLinkLabel()
    {
        var label = Match(LinkLabel());
        return label is null || label.Length > 1001 ? 0 : label.Length;
    }

    private bool ParseAutolink(MdNode block)
    {
        if (Match(EmailAutolink()) is { } email)
        {
            var address = email[1..^1];
            var link = new MdNode(MdType.Link) { Destination = MdSyntax.NormalizeUri("mailto:" + address) };
            link.Append(MdNode.Text(address));
            block.Append(link);
            return true;
        }

        if (Match(Autolink()) is { } uri)
        {
            var address = uri[1..^1];
            var link = new MdNode(MdType.Link) { Destination = MdSyntax.NormalizeUri(address) };
            link.Append(MdNode.Text(address));
            block.Append(link);
            return true;
        }

        return false;
    }

    private bool ParseHtmlTag(MdNode block)
    {
        // Each kind of tag needs its closing text after it; when none is left in the text, the
        // pattern isn't tried (it would scan to the end, again for every <).
        if (_lastClose.Gt < _pos) return false;
        if (Starts(_subject, _pos, "<!--") && _lastClose.Comment < _pos + 2 && !Starts(_subject, _pos, "<!-->") && !Starts(_subject, _pos, "<!--->")) return false;
        if (Starts(_subject, _pos, "<?") && _lastClose.Pi < _pos + 2) return false;
        if (Starts(_subject, _pos, "<![CDATA[") && _lastClose.Cdata < _pos + 9) return false;
        if (Match(HtmlTagHere()) is not { } tag) return false;
        block.Append(new MdNode(MdType.HtmlInline) { Literal = tag });
        return true;
    }

    private bool ParseEntity(MdNode block)
    {
        if (Match(EntityHere()) is not { } entity) return false;
        block.Append(MdNode.Text(MdSyntax.DecodeEntity(entity)));
        return true;
    }

    /// <summary>A link reference definition at <paramref name="start"/> in the text: its length, or 0 when there's none.</summary>
    public int ParseReference(string text, int start)
    {
        _subject = text;
        _pos = start;

        var labelLength = ParseLinkLabel();
        if (labelLength == 0) return 0;
        var rawLabel = _subject.Substring(start, labelLength);
        if (Peek() != ':')
        {
            _pos = start;
            return 0;
        }

        _pos++;
        SpaceNewline();
        var destination = ParseLinkDestination();
        if (destination is null)
        {
            _pos = start;
            return 0;
        }

        var beforeTitle = _pos;
        SpaceNewline();
        string? title = null;
        if (_pos != beforeTitle) title = ParseLinkTitle();
        if (title is null)
        {
            title = "";
            _pos = beforeTitle;
        }

        // The definition ends its line, or with the title dropped, the destination does.
        var atLineEnd = true;
        if (Match(SpaceAtEndOfLine()) is null)
        {
            if (title.Length == 0)
            {
                atLineEnd = false;
            }
            else
            {
                title = "";
                _pos = beforeTitle;
                atLineEnd = Match(SpaceAtEndOfLine()) is not null;
            }
        }

        if (!atLineEnd)
        {
            _pos = start;
            return 0;
        }

        var label = MdSyntax.NormalizeReference(rawLabel);
        if (label.Length == 0)
        {
            _pos = start;
            return 0;
        }

        refs.TryAdd(label, (destination, title));
        return _pos - start;
    }

    // GitHub's extended autolinks: www., http://, https:// and ftp:// addresses and email
    // addresses in text become links, outside links and code.
    private static void ExtendedAutolinks(MdNode root)
    {
        var containers = new Stack<MdNode>();
        containers.Push(root);
        while (containers.Count > 0)
        {
            for (var node = containers.Pop().FirstChild; node is not null; node = node.Next)
            {
                if (node.Type is MdType.Emph or MdType.Strong or MdType.Strike) containers.Push(node);
                if (node.Type != MdType.Text) continue;
                // Adjacent text runs are one text here.
                if (node.Next is { Type: MdType.Text })
                {
                    var merged = new System.Text.StringBuilder(node.Literal);
                    while (node.Next is { Type: MdType.Text } next)
                    {
                        merged.Append(next.Literal);
                        next.Unlink();
                    }

                    node.Literal = merged.ToString();
                }

                node = LinkText(node);
            }
        }
    }

    // Splits a text node at its autolinks; returns the last node made, for the walk to go on from.
    private static MdNode LinkText(MdNode node)
    {
        var text = node.Literal;
        var last = node;
        var from = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var (start, end, href) = FindAutolink(text, i, node);
            if (href is null || start < from) continue;
            if (start > from)
            {
                var before = MdNode.Text(text[from..start]);
                last.InsertAfter(before);
                last = before;
            }

            var link = new MdNode(MdType.Link) { Destination = MdSyntax.NormalizeUri(href) };
            link.Append(MdNode.Text(text[start..end]));
            last.InsertAfter(link);
            last = link;
            from = end;
            i = end - 1;
        }

        if (from == 0) return node;
        if (from < text.Length)
        {
            var rest = MdNode.Text(text[from..]);
            last.InsertAfter(rest);
            last = rest;
        }

        node.Unlink();
        return last;
    }

    private static (int Start, int End, string? Href) FindAutolink(string text, int i, MdNode node)
    {
        var c = text[i];
        if (c == '@') return FindEmail(text, i);

        // www. and scheme links start at a line's start, after white space, or after * _ ~ (.
        var boundary = i == 0
            ? node.Prev is null or { Type: MdType.SoftBreak or MdType.LineBreak or MdType.Emph or MdType.Strong or MdType.Strike }
              || node.Prev is { Type: MdType.Text } previous && previous.Literal.Length > 0 && Boundary(previous.Literal[^1])
            : Boundary(text[i - 1]);
        if (!boundary) return (0, 0, null);

        string prefix;
        if (Starts(text, i, "www.")) prefix = "http://";
        else if (Starts(text, i, "http://") || Starts(text, i, "https://") || Starts(text, i, "ftp://")) prefix = "";
        else return (0, 0, null);

        var domainStart = prefix.Length > 0 ? i : text.IndexOf("://", i, StringComparison.Ordinal) + 3;
        var domainEnd = domainStart;
        // No domain is longer than 253; scanning on would make a run of candidates quadratic.
        while (domainEnd < text.Length && domainEnd - domainStart <= 253 && (char.IsLetterOrDigit(text[domainEnd]) || text[domainEnd] is '-' or '_' or '.')) domainEnd++;
        if (domainEnd - domainStart > 253) return (0, 0, null);
        if (!ValidDomain(text[domainStart..domainEnd])) return (0, 0, null);

        var end = domainEnd;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '<') end++;
        end = TrimTrailing(text, i, end);
        if (end <= domainStart) return (0, 0, null);
        return (i, end, prefix + text[i..end]);
    }

    private static bool Boundary(char c) => char.IsWhiteSpace(c) || c is '*' or '_' or '~' or '(';

    private static bool Starts(string text, int i, string value) => string.CompareOrdinal(text, i, value, 0, value.Length) == 0;

    // Segments of letters, digits, _ and -, separated by periods; at least one period; no _ in the last two.
    private static bool ValidDomain(string domain)
    {
        domain = domain.TrimEnd('.');
        var segments = domain.Split('.');
        if (segments.Length < 2 || segments.Any(s => s.Length == 0)) return false;
        return !segments[^1].Contains('_') && !segments[^2].Contains('_');
    }

    // Trailing punctuation isn't part of a link; nor an unmatched ), nor an entity-like &name;.
    private static int TrimTrailing(string text, int start, int end)
    {
        (int Open, int Close)? parens = null;
        while (end > start)
        {
            var last = text[end - 1];
            if (last is '?' or '!' or '.' or ',' or ':' or '*' or '_' or '~' or '\'' or '"')
            {
                end--;
            }
            else if (last == ')')
            {
                // Counted once; each ) trimmed is one fewer.
                if (parens is null)
                {
                    var span = text.AsSpan(start, end - start);
                    parens = (span.Count('('), span.Count(')'));
                }

                if (parens.Value.Close <= parens.Value.Open) break;
                parens = (parens.Value.Open, parens.Value.Close - 1);
                end--;
            }
            else if (last == ';')
            {
                var amp = text.LastIndexOf('&', end - 1, end - start);
                if (amp < 0 || amp + 1 >= end - 1 || !text[(amp + 1)..(end - 1)].All(char.IsAsciiLetterOrDigit)) break;
                end = amp;
            }
            else
            {
                break;
            }
        }

        return end;
    }

    private static (int Start, int End, string? Href) FindEmail(string text, int at)
    {
        var start = at;
        while (start > 0 && (char.IsAsciiLetterOrDigit(text[start - 1]) || text[start - 1] is '.' or '-' or '_' or '+')) start--;
        if (start == at) return (0, 0, null);
        var end = at + 1;
        var periods = 0;
        while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] is '-' or '_' or '.'))
        {
            if (text[end] == '.') periods++;
            end++;
        }

        // A trailing period isn't part of it; a trailing - or _ means it isn't one.
        if (end > at + 1 && text[end - 1] == '.')
        {
            end--;
            periods--;
        }

        if (periods < 1 || end == at + 1 || text[end - 1] is '-' or '_') return (0, 0, null);
        var address = text[start..end];

        // mailto: and xmpp: before the address are part of the link; xmpp: may end in one /resource.
        if (start >= 7 && string.CompareOrdinal(text, start - 7, "mailto:", 0, 7) == 0) return (start - 7, end, "mailto:" + address);
        if (start >= 5 && string.CompareOrdinal(text, start - 5, "xmpp:", 0, 5) == 0)
        {
            if (end + 1 < text.Length && text[end] == '/' && (char.IsAsciiLetterOrDigit(text[end + 1]) || text[end + 1] is '@' or '.'))
            {
                end++;
                while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] is '@' or '.')) end++;
                if (text[end - 1] == '.') end--;
            }

            return (start - 5, end, text[(start - 5)..end]);
        }

        return (start, end, "mailto:" + address);
    }
}
