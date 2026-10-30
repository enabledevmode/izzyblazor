using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace IzzyBlazor;

internal sealed record XlsxColumn(string Header, double Width);

/// <summary>
/// A basic XLSX writer over System.IO.Compression: one sheet, a frozen header, typed cells. It streams
/// to a stream that needn't seek, with flat memory, and writes to it only asynchronously, as a web
/// response needs. Excel's own limits are enforced here: rows, cell length, and sheet size.
/// </summary>
internal static class XlsxWriter
{
    private const int MaxRows = 1_048_576, MaxColumns = 16_384, MaxCellChars = 32_767;
    private const int FlushAt = 64 * 1024;

    // Excel refused a sheet of 5.8 GB of XML and opened one of 3.6 GB; the limit is likely 4 GB.
    private const long MaxSheetBytes = 3_500_000_000;
    const int StyleDate = 1, StyleDateTime = 2, StyleHeader = 3;

    // Excel keeps 15 significant digits, and rounds the rest away.
    private const long MaxExactWhole = 999_999_999_999_999;

    public static async Task<DataExportResult> WriteAsync(
        Stream output, IReadOnlyList<XlsxColumn> columns, IAsyncEnumerator<object?[]> rows, bool more, string sheetName, string truncatedNote, CancellationToken ct)
    {
        const CompressionLevel level = CompressionLevel.Fastest;
        if (columns.Count is 0 or > MaxColumns) throw new ArgumentOutOfRangeException(nameof(columns));

        // .NET 10 ZipArchive still writes synchronously when an entry stream and the archive are
        // disposed, so everything goes through a stream that holds sync writes until DrainAsync.
        var sink = new AsyncSinkStream(output);
        var zip = await ZipArchive.CreateAsync(sink, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null, ct);
        // The archive is disposed on success only: disposing it after a failure would write a
        // central directory and turn a partial export into a well-formed zip.
        await WritePartAsync(zip, sink, "[Content_Types].xml", ContentTypes, level, ct);
        await WritePartAsync(zip, sink, "_rels/.rels", RootRels, level, ct);
        await WritePartAsync(zip, sink, "xl/workbook.xml", Workbook(sheetName), level, ct);
        await WritePartAsync(zip, sink, "xl/_rels/workbook.xml.rels", WorkbookRels, level, ct);
        await WritePartAsync(zip, sink, "xl/styles.xml", Styles, level, ct);

        var entry = zip.CreateEntry("xl/worksheets/sheet1.xml", level);
        var part = await entry.OpenAsync(ct);
        var w = new Utf8Buffer(FlushAt * 2);
        w.Ascii(SheetStart);
        w.Ascii("<cols>");
        for (var i = 0; i < columns.Count; i++)
        {
            w.Ascii($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{Math.Min(columns[i].Width, 255).ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
        }
        w.Ascii("</cols><sheetData>");

        var rowNumber = 1;
        w.RowStart(rowNumber);
        for (var i = 0; i < columns.Count; i++) w.Cell(i, rowNumber, columns[i].Header, StyleHeader);
        w.Ascii("</row>");

        long dataRows = 0;
        var truncated = false;
        for (; more; more = await rows.MoveNextAsync())
        {
            var row = rows.Current;
            // The last row says so, as a file that just ends looks complete.
            if (rowNumber == MaxRows - 1 || w.Written >= MaxSheetBytes)
            {
                truncated = true;
                rowNumber++;
                w.RowStart(rowNumber);
                w.Cell(0, rowNumber, truncatedNote, 0);
                w.Ascii("</row>");
                break;
            }

            rowNumber++;
            dataRows++;
            w.RowStart(rowNumber);
            var count = Math.Min(row.Length, MaxColumns);
            for (var i = 0; i < count; i++)
            {
                w.Value(i, rowNumber, row[i]);
                if (w.Length >= FlushAt) await w.FlushAsync(part, ct);
            }
            w.Ascii("</row>");
        }
        w.Ascii("</sheetData></worksheet>");
        await w.FlushAsync(part, ct);
        await part.DisposeAsync();
        await sink.DrainAsync(ct);

        await zip.DisposeAsync();
        await sink.DrainAsync(ct);
        await output.FlushAsync(ct);
        return new DataExportResult(dataRows, truncated);
    }

    static async Task WritePartAsync(ZipArchive zip, AsyncSinkStream sink, string name, string xml, CompressionLevel level, CancellationToken ct)
    {
        var entry = zip.CreateEntry(name, level);
        var s = await entry.OpenAsync(ct);
        await s.WriteAsync(Encoding.UTF8.GetBytes(xml), ct);
        await s.DisposeAsync();
        await sink.DrainAsync(ct);
    }

    /// <summary>Sheet names: 1 to 31 chars, none of \ / ? * [ ] :, no leading or trailing apostrophe.</summary>
    private static string SafeSheetName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name) sb.Append(c is '\\' or '/' or '?' or '*' or '[' or ']' or ':' || char.IsControl(c) ? '_' : c);
        var s = sb.ToString().Trim('\'');
        if (s.Length > 31) s = s[..(char.IsHighSurrogate(s[30]) ? 30 : 31)].Trim('\'');
        return s.Length == 0 ? "Sheet1" : s;
    }

    static string Workbook(string sheetName)
    {
        var name = new Utf8Buffer(256);
        name.Escaped(SafeSheetName(sheetName));
        return Xml + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
            + "<bookViews><workbookView/></bookViews>"
            + $"<sheets><sheet name=\"{name.ToAttribute()}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";
    }

    const string Xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n";

    const string ContentTypes = Xml + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
        + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
        + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
        + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
        + "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
        + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
        + "</Types>";

    const string RootRels = Xml + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
        + "</Relationships>";

    const string WorkbookRels = Xml + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>"
        + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
        + "</Relationships>";

    // cellXfs: 0 general, 1 date (built-in 14), 2 date and time (built-in 22), 3 bold header.
    const string Styles = Xml + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
        + "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font></fonts>"
        + "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>"
        + "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>"
        + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
        + "<cellXfs count=\"4\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"
        + "<xf numFmtId=\"14\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"
        + "<xf numFmtId=\"22\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"
        + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs>"
        + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"
        + "</styleSheet>";

    const string SheetStart = Xml + "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
        + "<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>"
        + "<selection pane=\"bottomLeft\" activeCell=\"A2\" sqref=\"A2\"/></sheetView></sheetViews>";

    /// <summary>1900 date system serial. Excel counts a fake 29 Feb 1900, so January and February 1900 use an epoch a day later.</summary>
    private static double? Serial(DateTime d)
    {
        // Excel's last serial is the end of 9999, which rounding the last second would pass.
        if (d < new DateTime(1900, 1, 1) || d >= new DateTime(9999, 12, 31, 23, 59, 59)) return null;
        var epoch = d < new DateTime(1900, 3, 1) ? new DateTime(1899, 12, 31) : new DateTime(1899, 12, 30);
        return (d - epoch).TotalDays;
    }

    sealed class Utf8Buffer(int capacity)
    {
        byte[] _b = new byte[capacity];
        int _n;
        public int Length => _n;

        public long Written { get; private set; }

        void Ensure(int extra)
        {
            if (_n + extra > _b.Length) Array.Resize(ref _b, Math.Max(_b.Length * 2, _n + extra));
        }

        public async ValueTask FlushAsync(Stream s, CancellationToken ct)
        {
            await s.WriteAsync(_b.AsMemory(0, _n), ct);
            Written += _n;
            _n = 0;
        }

        public string ToAttribute() => Encoding.UTF8.GetString(_b, 0, _n).Replace("\"", "&quot;");

        public void Ascii(string s)
        {
            Ensure(s.Length);
            for (var i = 0; i < s.Length; i++) _b[_n++] = (byte)s[i];
        }

        void Int(long v)
        {
            Ensure(24);
            v.TryFormat(_b.AsSpan(_n), out var w, default, CultureInfo.InvariantCulture);
            _n += w;
        }

        void Ref(int col, int row)
        {
            Ensure(8);
            Span<byte> t = stackalloc byte[3];
            var k = 0;
            for (var c = col + 1; c > 0; c = (c - 1) / 26) t[k++] = (byte)('A' + (c - 1) % 26);
            while (k > 0) _b[_n++] = t[--k];
            Int(row);
        }

        public void RowStart(int row)
        {
            Ascii("<row r=\"");
            Int(row);
            Ascii("\">");
        }

        void Open(int col, int row, int style, string? type)
        {
            Ascii("<c r=\"");
            Ref(col, row);
            if (style != 0) { Ascii("\" s=\""); Int(style); }
            if (type != null) { Ascii("\" t=\""); Ascii(type); }
            Ascii("\">");
        }

        public void Value(int col, int row, object? value)
        {
            switch (value)
            {
                case null: break;
                case string s: Cell(col, row, s, 0); break;
                case bool b: Open(col, row, 0, "b"); Ascii(b ? "<v>1</v></c>" : "<v>0</v></c>"); break;
                case sbyte or byte or short or ushort or int or long:
                    var whole = Convert.ToInt64(value);
                    if (whole is >= -MaxExactWhole and <= MaxExactWhole) Number(col, row, whole); else Cell(col, row, whole.ToString(CultureInfo.InvariantCulture), 0);
                    break;
                case uint u: Number(col, row, u); break;
                case ulong u when u <= MaxExactWhole: Number(col, row, u); break;
                case ulong u: Cell(col, row, u.ToString(CultureInfo.InvariantCulture), 0); break;
                case float f when !float.IsFinite(f): Cell(col, row, f.ToString(CultureInfo.InvariantCulture), 0); break;
                case double d when !double.IsFinite(d): Cell(col, row, d.ToString(CultureInfo.InvariantCulture), 0); break;
                case float f: Number(col, row, f); break;
                case double d: Number(col, row, d); break;
                case decimal d: Number(col, row, d); break;
                case DateOnly d: Date(col, row, d.ToDateTime(TimeOnly.MinValue), StyleDate, "yyyy-MM-dd"); break;
                case DateTime d: Date(col, row, d, StyleDateTime, "yyyy-MM-dd HH:mm:ss"); break;
                case DateTimeOffset d: Date(col, row, d.DateTime, StyleDateTime, "yyyy-MM-dd HH:mm:ss"); break;
                case IFormattable f: Cell(col, row, f.ToString(null, CultureInfo.InvariantCulture), 0); break;
                default: Cell(col, row, value.ToString() ?? "", 0); break;
            }
        }

        void Date(int col, int row, DateTime d, int style, string textFormat)
        {
            if (Serial(d) is double serial) Number(col, row, serial, style);
            else Cell(col, row, d.ToString(textFormat, CultureInfo.InvariantCulture), 0); // No serial before 1900.
        }

        void Number<T>(int col, int row, T v, int style = 0) where T : IUtf8SpanFormattable
        {
            Open(col, row, style, null);
            Ascii("<v>");
            Ensure(64);
            v.TryFormat(_b.AsSpan(_n), out var w, default, CultureInfo.InvariantCulture);
            _n += w;
            Ascii("</v></c>");
        }

        public void Cell(int col, int row, string text, int style)
        {
            Open(col, row, style, "inlineStr");
            if (text.Length > MaxCellChars)
            {
                var cut = char.IsHighSurrogate(text[MaxCellChars - 1]) ? MaxCellChars - 1 : MaxCellChars;
                text = text[..cut];
            }
            var keep = text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]));
            Ascii(keep ? "<is><t xml:space=\"preserve\">" : "<is><t>");
            Escaped(text);
            Ascii("</t></is></c>");
        }

        static bool IsEscapeAt(string s, int i)
        {
            // A control character right after is written starting with the underscore that would close it.
            if (i + 6 >= s.Length || s[i + 1] != 'x' || (s[i + 6] != '_' && !IsEscaped(s[i + 6]))) return false;
            for (var k = i + 2; k < i + 6; k++) if (!char.IsAsciiHexDigit(s[k])) return false;
            return true;
        }

        static bool IsEscaped(char c) => (c < 0x20 && c is not ('\t' or '\n')) || c is '￾' or '￿';

        /// <summary>XML text plus the ST_Xstring rule: controls become _xHHHH_, and text that already looks like that gets its underscore escaped.</summary>
        public void Escaped(string s)
        {
            Ensure(s.Length * 7 + 8); // Worst case is a control char: 1 UTF-16 unit, 7 bytes.
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                switch (c)
                {
                    case '&': Ascii("&amp;"); continue;
                    case '<': Ascii("&lt;"); continue;
                    case '>': Ascii("&gt;"); continue;
                    case '_' when IsEscapeAt(s, i): Ascii("_x005F_"); continue;
                    case '\t' or '\n': _b[_n++] = (byte)c; continue;
                }
                if (IsEscaped(c)) Ascii($"_x{(int)c:X4}_");
                else if (c < 0x80) _b[_n++] = (byte)c;
                else if (char.IsSurrogate(c))
                {
                    if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                    {
                        new Rune(c, s[++i]).EncodeToUtf8(_b.AsSpan(_n));
                        _n += 4;
                    }
                    else { Rune.ReplacementChar.EncodeToUtf8(_b.AsSpan(_n)); _n += 3; } // Unpaired: not representable.
                }
                else _n += new Rune(c).EncodeToUtf8(_b.AsSpan(_n));
            }
        }
    }

    /// <summary>Forward-only output: sync writes wait in memory until DrainAsync; async writes pass straight through.</summary>
    private sealed class AsyncSinkStream(Stream inner) : Stream
    {
        readonly MemoryStream _held = new();

        public async ValueTask DrainAsync(CancellationToken ct)
        {
            if (_held.Length == 0) return;
            await inner.WriteAsync(_held.GetBuffer().AsMemory(0, (int)_held.Length), ct);
            _held.SetLength(0);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _held.Write(buffer);
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            await DrainAsync(ct);
            await inner.WriteAsync(buffer, ct);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            WriteAsync(buffer.AsMemory(offset, count), ct).AsTask();

        public override void Flush() { }
        public override async Task FlushAsync(CancellationToken ct) { await DrainAsync(ct); await inner.FlushAsync(ct); }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
