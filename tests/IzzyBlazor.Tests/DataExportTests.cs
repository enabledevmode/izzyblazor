using System.IO.Compression;
using System.Linq.Expressions;
using System.Text;
using System.Xml.Linq;

namespace IzzyBlazor.Tests;

public class DataExportTests
{
    public enum Kind { First, Second }

    public sealed record Row(string Name, int Qty, decimal Price, DateOnly Day, DateTime At, bool Done, Kind Kind, string? Note);

    private static readonly Row[] Rows =
    [
        new("Ada, Lovelace", 2, 1.5m, new DateOnly(2026, 10, 30), new DateTime(2026, 10, 30, 13, 30, 0), true, Kind.Second, "=1+1"),
        new("Grace \"Amazing\" Hopper", -3, 2m, new DateOnly(1900, 3, 1), new DateTime(1800, 1, 1), false, Kind.First, null),
        new("Alan", 1, 0.25m, new DateOnly(2000, 1, 1), new DateTime(2000, 1, 1), false, Kind.First, "a_x0041_b\u0001&<>"),
    ];

    private static IReadOnlyList<DataField<Row>> Fields => DataFields.Of<Row>();

    // A web response: it can't seek, and a synchronous write fails the request.
    private sealed class ResponseStream : Stream
    {
        private readonly MemoryStream _written = new();

        public byte[] ToArray() => _written.ToArray();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous operations are disallowed.");

        public override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException("Synchronous operations are disallowed.");

        public override void WriteByte(byte value) => throw new InvalidOperationException("Synchronous operations are disallowed.");

        public override void Flush() => throw new InvalidOperationException("Synchronous operations are disallowed.");

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _written.Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _written.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static async Task<string> CsvAsync(string? query, bool escape = true)
    {
        var stream = new ResponseStream();
        await DataExport.WriteCsvAsync(stream, Rows.AsQueryable(), query, Fields, escape);
        var bytes = stream.ToArray();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        return Encoding.UTF8.GetString(bytes[3..]);
    }

    private static async Task<(XElement Sheet, ZipArchive Zip, DataExportResult Result)> ExcelAsync(string? query, IQueryable<Row>? source = null)
    {
        var stream = new ResponseStream();
        var result = await DataExport.WriteExcelAsync(stream, source ?? Rows.AsQueryable(), query, Fields, "Orders: [all]");
        var zip = new ZipArchive(new MemoryStream(stream.ToArray()));
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        return (XDocument.Load(sheet).Root!, zip, result);
    }

    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static XElement Cell(XElement sheet, string reference) =>
        sheet.Descendants(Ns + "c").Single(c => (string?)c.Attribute("r") == reference);

    [Fact]
    public async Task Csv_has_a_header_and_quotes_what_needs_it()
    {
        var lines = (await CsvAsync("columns=name,qty")).Split("\r\n");
        Assert.Equal("Name,Qty", lines[0]);
        Assert.Equal("\"Ada, Lovelace\",2", lines[1]);
        Assert.Equal("\"Grace \"\"Amazing\"\" Hopper\",-3", lines[2]);
    }

    [Fact]
    public async Task Csv_escapes_formulas_in_text_and_not_negative_numbers_unless_told_not_to()
    {
        var text = await CsvAsync("columns=qty,note");
        Assert.Contains("2,'=1+1", text);
        Assert.Contains("-3,", text);
        Assert.Contains("2,=1+1", await CsvAsync("columns=qty,note", escape: false));
    }

    [Fact]
    public async Task The_filter_sort_search_and_columns_of_the_address_are_applied_and_paging_isnt()
    {
        var lines = (await CsvAsync("filter=qty%20gt%200&sort=-name&columns=qty,name&skip=1&take=1")).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Qty,Name", "1,Alan", "2,\"Ada, Lovelace\""], lines);

        Assert.Equal(2, (await CsvAsync("search=grace&columns=name")).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task A_field_the_server_doesnt_list_is_never_written()
    {
        var text = await CsvAsync("columns=name,secret");
        Assert.StartsWith("Name\r\n", text);

        // Asking for nothing it knows gets what it would have got by asking for nothing.
        Assert.StartsWith("Name,Qty,Price,", await CsvAsync("columns=secret"));
    }

    [Fact]
    public async Task Excel_writes_typed_cells_to_a_stream_that_only_takes_async_writes()
    {
        var (sheet, zip, result) = await ExcelAsync("columns=name,qty,price,day,at,done,kind,note");
        using (zip)
        {
            Assert.Equal(new DataExportResult(3, false), result);
            Assert.Equal("Name", Cell(sheet, "A1").Value);
            Assert.Equal("3", (string?)Cell(sheet, "A1").Attribute("s"));
            Assert.Equal("Ada, Lovelace", Cell(sheet, "A2").Value);
            Assert.Equal("inlineStr", (string?)Cell(sheet, "A2").Attribute("t"));
            Assert.Equal("2", Cell(sheet, "B2").Value);
            Assert.Null(Cell(sheet, "B2").Attribute("t"));
            Assert.Equal("1.5", Cell(sheet, "C2").Value);
            Assert.Equal("46325", Cell(sheet, "D2").Value);
            Assert.Equal("1", (string?)Cell(sheet, "D2").Attribute("s"));
            Assert.Equal("b", (string?)Cell(sheet, "F2").Attribute("t"));
            Assert.Equal("Second", Cell(sheet, "G2").Value);
            // Text in a cell is text: no formula.
            Assert.Equal("=1+1", Cell(sheet, "H2").Value);
            Assert.Empty(sheet.Descendants(Ns + "f"));
            // Before 1900 there's no serial, so it's text.
            Assert.Equal("1800-01-01 00:00:00", Cell(sheet, "E3").Value);
            // A value that looks like Excel's own escape stays as it was, and control characters survive.
            Assert.Equal("a_x005F_x0041_b_x0001_&<>", Cell(sheet, "H4").Value);
            Assert.NotNull(sheet.Descendants(Ns + "pane").Single());
        }
    }

    [Fact]
    public async Task Excel_has_the_parts_a_workbook_needs_and_a_safe_sheet_name()
    {
        var (_, zip, _) = await ExcelAsync(null);
        using (zip)
        {
            foreach (var part in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml" })
            {
                Assert.NotNull(zip.GetEntry(part));
            }

            using var workbook = zip.GetEntry("xl/workbook.xml")!.Open();
            Assert.Equal("Orders_ _all_", (string?)XDocument.Load(workbook).Descendants(Ns + "sheet").Single().Attribute("name"));
        }
    }

    [Fact]
    public async Task Excel_stops_at_what_a_sheet_can_hold_and_says_so_in_the_sheet()
    {
        var many = Enumerable.Repeat(Rows[2], 1_048_580).AsQueryable();
        var stream = new ResponseStream();
        var result = await DataExport.WriteExcelAsync(stream, many, "columns=qty", Fields, truncatedNote: "There's more.");
        Assert.True(result.Truncated);
        Assert.Equal(1_048_574, result.Rows);

        using var zip = new ZipArchive(new MemoryStream(stream.ToArray()));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        Assert.EndsWith("<row r=\"1048576\"><c r=\"A1048576\" t=\"inlineStr\"><is><t>There's more.</t></is></c></row></sheetData></worksheet>", await reader.ReadToEndAsync());
    }

    public sealed record Owner(string Name);

    public sealed record Owned(Owner? Owner, long Big, ulong Bigger, DateTime Until, string Text);

    private static readonly Expression<Func<Owned, string?>> OwnerName = o => o.Owner!.Name;

    private static readonly IReadOnlyList<DataField<Owned>> OwnedFields = [.. DataFields.Of<Owned>(), new DataField<Owned> { Key = "ownername", Value = OwnerName }];

    [Fact]
    public async Task A_null_along_the_way_is_an_empty_cell_not_a_failed_export()
    {
        Owned[] rows = [new(null, 1, 1, DateTime.MinValue, "a"), new(new("Ann"), 1, 1, DateTime.MinValue, "b")];
        var stream = new ResponseStream();
        var result = await DataExport.WriteCsvAsync(stream, rows.AsQueryable(), "columns=text,ownername", OwnedFields);
        Assert.Equal(2, result.Rows);
        var lines = Encoding.UTF8.GetString(stream.ToArray()[3..]).Split("\r\n");
        Assert.Equal(["a,", "b,Ann"], lines[1..3]);
    }

    [Fact]
    public async Task Csv_quotes_semicolons_and_tabs_and_keeps_an_empty_lone_cell_as_a_row()
    {
        Owned[] rows = [new(null, 1, 1, DateTime.MinValue, "a;=1+1"), new(null, 1, 1, DateTime.MinValue, "b\t=1"), new(null, 1, 1, DateTime.MinValue, "")];
        var stream = new ResponseStream();
        await DataExport.WriteCsvAsync(stream, rows.AsQueryable(), "columns=text", OwnedFields);
        Assert.Equal("Text\r\n\"a;=1+1\"\r\n\"b\t=1\"\r\n\"\"\r\n", Encoding.UTF8.GetString(stream.ToArray()[3..]));
    }

    [Fact]
    public async Task Excel_keeps_what_its_numbers_and_dates_cant_hold_as_text_and_widens_its_columns()
    {
        Owned[] rows = [new(null, 1234567890123456789, 12345678901234567890, DateTime.MaxValue, "_x0041\u0001")];
        var stream = new ResponseStream();
        await DataExport.WriteExcelAsync(stream, rows.AsQueryable(), "columns=big,bigger,until,text", OwnedFields, new string([.. string.Concat(Enumerable.Repeat("😀", 20))]));
        using var zip = new ZipArchive(new MemoryStream(stream.ToArray()));
        using var part = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var sheet = XDocument.Load(part).Root!;
        Assert.Equal("1234567890123456789", Cell(sheet, "A2").Value);
        Assert.Equal("inlineStr", (string?)Cell(sheet, "A2").Attribute("t"));
        Assert.Equal("12345678901234567890", Cell(sheet, "B2").Value);
        Assert.Equal("inlineStr", (string?)Cell(sheet, "C2").Attribute("t"));
        // Its underscore is escaped, or the control character's own would close it as an escape.
        Assert.Equal("_x005F_x0041_x0001_", Cell(sheet, "D2").Value);
        Assert.Equal(["18", "18", "19", "24"], sheet.Descendants(Ns + "col").Select(c => (string)c.Attribute("width")!));

        using var book = zip.GetEntry("xl/workbook.xml")!.Open();
        var name = (string)XDocument.Load(book).Descendants(Ns + "sheet").Single().Attribute("name")!;
        Assert.Equal(string.Concat(Enumerable.Repeat("😀", 15)), name);
    }

    [Fact]
    public async Task A_full_address_is_read_the_same_as_its_query()
    {
        var text = await CsvAsync("http://site/o.csv?columns=qty&sort=qty");
        Assert.StartsWith("Qty\r\n-3\r\n", text);
    }

    [Fact]
    public async Task A_query_that_fails_at_once_writes_nothing_so_the_app_can_answer_with_an_error()
    {
        IEnumerable<Row> Failing()
        {
            throw new InvalidOperationException("The database went away.");
#pragma warning disable CS0162 // It's an iterator because of this.
            yield break;
#pragma warning restore CS0162
        }

        var stream = new ResponseStream();
        await Assert.ThrowsAsync<InvalidOperationException>(() => DataExport.WriteCsvAsync(stream, Failing().AsQueryable(), null, Fields));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DataExport.WriteExcelAsync(stream, Failing().AsQueryable(), null, Fields));
        Assert.Empty(stream.ToArray());
    }

    [Fact]
    public async Task An_export_needs_a_field()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => DataExport.WriteCsvAsync(new ResponseStream(), Rows.AsQueryable(), null, []));
    }

    // What a database's query is: filtered and sorted through its provider, and read asynchronously.
    private sealed class AsyncQuery<T>(IQueryable<T> inner, List<CancellationToken> tokens) : IQueryable<T>, IAsyncEnumerable<T>, IQueryProvider
    {
        public Type ElementType => inner.ElementType;

        public Expression Expression => inner.Expression;

        public IQueryProvider Provider => this;

        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("Read synchronously.");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            tokens.Add(cancellationToken);
            foreach (var item in inner)
            {
                await Task.Yield();
                yield return item;
            }
        }

        public IQueryable<E> CreateQuery<E>(Expression expression) => new AsyncQuery<E>(inner.Provider.CreateQuery<E>(expression), tokens);

        public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();

        public object? Execute(Expression expression) => throw new NotSupportedException();

        public E Execute<E>(Expression expression) => throw new NotSupportedException();
    }

    [Fact]
    public async Task A_database_is_read_asynchronously_with_the_token_in_both_formats()
    {
        using var cts = new CancellationTokenSource();
        var tokens = new List<CancellationToken>();
        var source = new AsyncQuery<Row>(Rows.AsQueryable(), tokens);

        Assert.Equal(3, (await DataExport.WriteCsvAsync(new ResponseStream(), source, "columns=name", Fields, cancellationToken: cts.Token)).Rows);
        Assert.Equal(3, (await DataExport.WriteExcelAsync(new ResponseStream(), source, "columns=name", Fields, cancellationToken: cts.Token)).Rows);
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t => Assert.Equal(cts.Token, t));
    }

    [Fact]
    public async Task A_failure_part_way_leaves_an_unreadable_file_not_one_that_looks_complete()
    {
        IEnumerable<Row> Failing()
        {
            yield return Rows[0];
            throw new InvalidOperationException("The database went away.");
        }

        var stream = new ResponseStream();
        await Assert.ThrowsAsync<InvalidOperationException>(() => DataExport.WriteExcelAsync(stream, Failing().AsQueryable(), null, Fields));
        Assert.ThrowsAny<Exception>(() => new ZipArchive(new MemoryStream(stream.ToArray())));
    }
}
