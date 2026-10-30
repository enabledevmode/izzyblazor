using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

namespace IzzyBlazor;

/// <summary>What an export wrote.</summary>
/// <param name="Rows">The rows of data written, not counting the header.</param>
/// <param name="Truncated">Whether it stopped early, at what the format can hold, with a note in the file's last row. An Excel sheet holds 1,048,576 rows.</param>
public readonly record struct DataExportResult(long Rows, bool Truncated);

/// <summary>
/// Writes a query's rows as CSV or Excel to a stream, row by row, so memory stays flat however many
/// there are. It runs on an endpoint the app maps, which a <c>DataGrid</c>'s export links go to with
/// the filter, sort, search and visible columns of what's on screen:
/// <code>
/// app.MapGet("/orders/export.csv", (HttpContext http, AppDb db) =&gt; Results.Stream(
///     stream =&gt; DataExport.WriteCsvAsync(stream, db.Orders.AsNoTracking(), http.Request.QueryString.Value, DataFields.Of&lt;Order&gt;(), cancellationToken: http.RequestAborted),
///     "text/csv", "orders.csv"));
/// </code>
/// The fields decide what can be filtered, sorted, searched and written, whatever the address says.
/// A database reads only those columns, and a null along a navigation is an empty cell. A field it
/// can't translate, such as a computed property, loads whole rows, which <c>AsNoTracking</c> keeps
/// from filling memory, and throws on such a null; so does a field alone that doesn't read the row.
/// A query that fails at the start throws before anything is written, so the app can still answer
/// with an error; one that fails part way breaks the download.
/// </summary>
public static class DataExport
{
    /// <summary>
    /// Writes CSV in UTF-8 with a byte order mark, which Excel needs to read it as UTF-8. Values use
    /// each field's <see cref="DataField{TItem}.Format"/>, in the current culture.
    /// </summary>
    /// <param name="output">Where it goes.</param>
    /// <param name="source">The rows, before filtering. An <c>IQueryable</c> is run by the database.</param>
    /// <param name="query">The address's query string: <c>filter</c>, <c>sort</c>, <c>search</c> and <c>columns</c> (keys, in order; all the fields if left out, or if it names none of them).</param>
    /// <param name="fields">What can be written, filtered, sorted and searched. Their captions head the file.</param>
    /// <param name="escapeFormulas">Puts an apostrophe before text starting with <c>=</c>, <c>+</c>, <c>-</c> or <c>@</c>, which a spreadsheet would run as a formula. Leave it on for anything people typed.</param>
    /// <param name="cancellationToken">Stops the export, such as when the download is abandoned.</param>
    public static async Task<DataExportResult> WriteCsvAsync<TItem>(Stream output, IQueryable<TItem> source, string? query, IReadOnlyList<DataField<TItem>> fields, bool escapeFormulas = true, CancellationToken cancellationToken = default)
    {
        var columns = ColumnsOf(query, fields);
        await using var rows = Rows(source, query, fields, columns, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var more = await rows.MoveNextAsync();

        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), bufferSize: 64 * 1024, leaveOpen: true);
        await writer.WriteAsync(string.Join(',', columns.Select(c => CsvText(c.Caption, escapeFormulas))).AsMemory(), cancellationToken);
        await writer.WriteAsync("\r\n".AsMemory(), cancellationToken);

        long count = 0;
        for (; more; more = await rows.MoveNextAsync())
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (i > 0)
                {
                    await writer.WriteAsync(',');
                }

                var cell = CsvCell(rows.Current[i], columns[i].Format, escapeFormulas);
                // Alone on its line, an empty cell would be a blank line, which readers skip.
                await writer.WriteAsync((columns.Count == 1 && cell.Length == 0 ? "\"\"" : cell).AsMemory(), cancellationToken);
            }

            await writer.WriteAsync("\r\n".AsMemory(), cancellationToken);
            count++;
        }

        await writer.FlushAsync(cancellationToken);
        return new DataExportResult(count, false);
    }

    /// <summary>
    /// Writes an Excel workbook of one sheet: a bold, frozen header, then typed cells (numbers, dates,
    /// booleans and text). Basic by design: no field formats, styles or formulas. A failure part way
    /// leaves the file unreadable rather than looking complete. Whole numbers past 15 digits are text,
    /// which Excel would otherwise round; a <c>DateTimeOffset</c> is its clock time; text past Excel's
    /// 32,767 characters is cut.
    /// </summary>
    /// <param name="output">Where it goes. It needn't seek, and nothing is written to it synchronously.</param>
    /// <param name="source">The rows, before filtering. An <c>IQueryable</c> is run by the database.</param>
    /// <param name="query">The address's query string: <c>filter</c>, <c>sort</c>, <c>search</c> and <c>columns</c> (keys, in order; all the fields if left out, or if it names none of them).</param>
    /// <param name="fields">What can be written, filtered, sorted and searched. Their captions head the file.</param>
    /// <param name="sheetName">The sheet's name.</param>
    /// <param name="truncatedNote">The last row, when there's more than a sheet can hold.</param>
    /// <param name="cancellationToken">Stops the export, such as when the download is abandoned.</param>
    public static async Task<DataExportResult> WriteExcelAsync<TItem>(Stream output, IQueryable<TItem> source, string? query, IReadOnlyList<DataField<TItem>> fields, string sheetName = "Sheet1", string truncatedNote = "This export stopped here: there are more rows than a sheet can hold.", CancellationToken cancellationToken = default)
    {
        var columns = ColumnsOf(query, fields);
        await using var rows = Rows(source, query, fields, columns, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var more = await rows.MoveNextAsync();
        return await XlsxWriter.WriteAsync(
            output,
            columns.Select(c => new XlsxColumn(c.Caption, ExcelWidth(c))).ToList(),
            rows,
            more,
            sheetName,
            truncatedNote,
            cancellationToken);
    }

    private sealed record Column<TItem>(string Caption, string? Format, LambdaExpression Value);

    // The columns asked for, of the fields there are: a key the fields don't have isn't written. With
    // none of them known (a grid of only template columns), all of the fields, not a file of nothing.
    private static List<Column<TItem>> ColumnsOf<TItem>(string? query, IReadOnlyList<DataField<TItem>> fields)
    {
        if (fields.Count == 0)
        {
            throw new ArgumentException("An export needs at least one field.", nameof(fields));
        }

        var at = query?.IndexOf('?', StringComparison.Ordinal) ?? -1;
        var asked = query is null ? null : QueryString.Parse(query[(at + 1)..]).GetValueOrDefault("columns").ToString();
        var known = (asked ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(key => fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase)))
            .OfType<DataField<TItem>>()
            .Distinct()
            .ToList();
        return (known.Count == 0 ? fields : known).Select(f => new Column<TItem>(f.Caption ?? f.Key, f.Format, f.Value)).ToList();
    }

    // Wide enough for its heading and, as Excel shows ##### for a date that doesn't fit, its type.
    private static double ExcelWidth<TItem>(Column<TItem> column)
    {
        var type = Nullable.GetUnderlyingType(column.Value.ReturnType) ?? column.Value.ReturnType;
        double width = type == typeof(DateOnly) ? 12 : type == typeof(DateTime) || type == typeof(DateTimeOffset) ? 19 : type == typeof(string) ? 24 : type == typeof(long) || type == typeof(ulong) ? 18 : 12;
        return Math.Max(width, column.Caption.Length + 3);
    }

    // Filtered and sorted, not paged: an export is everything that matches.
    private static async IAsyncEnumerable<object?[]> Rows<TItem>(IQueryable<TItem> source, string? query, IReadOnlyList<DataField<TItem>> fields, List<Column<TItem>> columns, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = DataRequest<TItem>.Parse(query ?? "");
        var rows = request.ApplySorting(request.ApplyFiltering(source, fields), fields);

        // A database selects the columns: nothing else is loaded or tracked, and a null along the way is null.
        if (rows.Select(Projection(columns)) is IAsyncEnumerable<object?[]> selected)
        {
            await foreach (var row in selected.WithCancellation(cancellationToken))
            {
                yield return row;
            }

            yield break;
        }

        // In memory a null along the way throws, cell by cell, so it's made null to match. A field
        // that throws the same by itself looks no different.
        var readers = columns.Select(c => Expression.Lambda<Func<TItem, object?>>(Expression.Convert(c.Value.Body, typeof(object)), c.Value.Parameters).Compile()).ToList();
        foreach (var item in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = new object?[readers.Count];
            for (var i = 0; i < values.Length; i++)
            {
                try
                {
                    values[i] = readers[i](item);
                }
                catch (NullReferenceException)
                {
                    values[i] = null;
                }
            }

            yield return values;
        }
    }

    private static Expression<Func<TItem, object?[]>> Projection<TItem>(List<Column<TItem>> columns)
    {
        var item = Expression.Parameter(typeof(TItem), "item");
        var values = columns.Select(c => Expression.Convert(new Rebinder(c.Value.Parameters[0], item).Visit(c.Value.Body), typeof(object)));
        return Expression.Lambda<Func<TItem, object?[]>>(Expression.NewArrayInit(typeof(object), values), item);
    }

    private sealed class Rebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }

    private static string CsvCell(object? value, string? format, bool escapeFormulas) => value switch
    {
        null => "",
        string text => CsvText(text, escapeFormulas),
        // A number can start with a minus sign and stay a number.
        IFormattable formattable => CsvText(formattable.ToString(format, CultureInfo.CurrentCulture), escapeFormulas && formattable is not (sbyte or short or int or long or Int128 or float or double or decimal)),
        _ => CsvText(value.ToString() ?? "", escapeFormulas),
    };

    private static string CsvText(string text, bool escapeFormulas)
    {
        if (escapeFormulas && text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' or '＝' or '＋' or '－' or '＠')
        {
            text = "'" + text;
        }

        // Semicolons and tabs too: where the list separator is one, Excel splits on it, and what
        // follows would start a cell of its own, past the check above.
        return text.AsSpan().IndexOfAny(",;\t\"\r\n") >= 0 || text.Length != text.Trim().Length ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : text;
    }
}
