using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

// A view's column bands: headers over the columns inside them.
public abstract partial class TableViewBase<TItem> : IColumnBandHost
{
    // Its columns and bands as the page has them, each band holding its own; the columns' order is
    // read from this, as a band's columns join after the columns beside it.
    private readonly List<object> _nodes = [];
    private int _bands;
    private int _bandsWaiting;
    private TaskCompletionSource? _bandsJoined;

    private List<ColumnBand>? _bandOrder;

    /// <summary>The columns and bands as the page has them, each band holding its own.</summary>
    internal IReadOnlyList<object> BandNodes => _nodes;

    /// <summary>A band's number among them all, in page order: the script names it by this.</summary>
    internal int BandId(ColumnBand band)
    {
        if (_bandOrder is null)
        {
            _bandOrder = [];
            Collect(_nodes);
        }

        return _bandOrder.IndexOf(band);

        void Collect(List<object> nodes)
        {
            foreach (var node in nodes.OfType<ColumnBand>())
            {
                _bandOrder!.Add(node);
                Collect(node.Nodes);
            }
        }
    }

    /// <summary>The numbers of the bands a column is in, outermost first: "0.2", or null in none.</summary>
    internal string? BandPath(ColumnBase<TItem> column) => column.Band is { } band ? BandPath(band) : null;

    /// <summary>The numbers of a band's bands, ending with its own.</summary>
    internal string BandPath(ColumnBand band) => string.Join('.', band.Chain.Select(BandId));

    /// <summary>A column's name with its bands', for where it's named away from its header: "Q1 Revenue".</summary>
    internal string FullName(ColumnBase<TItem> column)
    {
        var name = column.HeaderText;
        for (var band = column.Band; band is not null; band = band.Parent)
        {
            name = string.Format(System.Globalization.CultureInfo.CurrentCulture, TextDefaults.ColumnInBand, band.Title, name);
        }

        return name;
    }

    /// <summary>Whether the band row can be hidden, so each header names its bands (a stacked table).</summary>
    internal virtual bool StacksBands => false;

    /// <summary>A band's name with the bands it's in: "2026 Q1".</summary>
    internal string BandFullName(ColumnBand band) => band.Parent is { } parent
        ? string.Format(System.Globalization.CultureInfo.CurrentCulture, TextDefaults.ColumnInBand, BandFullName(parent), band.Title)
        : band.Title ?? "";

    /// <summary>Whether any column is in a band.</summary>
    internal bool HasBands => _bands > 0;

    /// <summary>How many rows of bands the header has, over the columns shown.</summary>
    internal int BandDepth => HasBands && Shown.Count > 0 ? Shown.Max(c => c.Band?.Chain.Count ?? 0) : 0;

    /// <summary>A run of columns with one band over them at a level, or a column with none.</summary>
    internal readonly record struct BandSpan(ColumnBand? Band, int First, int Count)
    {
        public int Last => First + Count - 1;
    }

    /// <summary>
    /// A row of bands over the columns shown, from 0 at the top: a span per band, and one per column
    /// with none there. A band sits as low as the bands inside it let it, so a band with none is right
    /// above its columns, and each band has one row, over all its columns, whatever else it holds.
    /// </summary>
    internal List<BandSpan> BandRow(int level)
    {
        // How many rows each band and those inside it take, over the columns shown.
        var height = new Dictionary<ColumnBand, int>();
        foreach (var column in Shown)
        {
            var chain = column.Band?.Chain ?? [];
            for (var i = 0; i < chain.Count; i++)
            {
                height[chain[i]] = Math.Max(height.GetValueOrDefault(chain[i]), chain.Count - i);
            }
        }

        var depth = BandDepth;
        return Spans(chain => chain.FirstOrDefault(b => depth - height[b] == level));
    }

    /// <summary>The outermost bands, and a span per column in none: the table's column groups.</summary>
    internal List<BandSpan> OuterBands() => Spans(chain => chain.Count > 0 ? chain[0] : null);

    private List<BandSpan> Spans(Func<IReadOnlyList<ColumnBand>, ColumnBand?> pick)
    {
        var spans = new List<BandSpan>();
        var shown = Shown;
        for (var i = 0; i < shown.Count; i++)
        {
            var band = pick(shown[i].Band?.Chain ?? []);
            if (band is not null && spans.Count > 0 && spans[^1].Band == band && !BandBreaks(shown[i - 1], shown[i]))
            {
                spans[^1] = spans[^1] with { Count = spans[^1].Count + 1 };
            }
            else
            {
                spans.Add(new(band, i, 1));
            }
        }

        return spans;
    }

    /// <summary>Whether a band can't go on from one column to the next, such as across a frozen edge.</summary>
    internal virtual bool BandBreaks(ColumnBase<TItem> before, ColumnBase<TItem> after) => false;

    /// <summary>A grid's classes for a band's header cell over these columns: frozen, hidden.</summary>
    internal virtual string? BandColumnsClass(BandSpan span) => null;

    /// <summary>A grid's style for a band's header cell over these columns: a frozen one's offset.</summary>
    internal virtual string? BandStyle(BandSpan span) => null;

    internal string BandClass(ColumnBand band, BandSpan span)
    {
        var align = band.Align switch
        {
            CellAlign.Center => "ib-cell-center",
            CellAlign.End => "ib-cell-end",
            _ => null,
        };
        return string.Join(' ', new[] { "ib-table-band", align, band.Class, BandColumnsClass(span) }.Where(c => !string.IsNullOrEmpty(c)));
    }

    /// <summary>The empty cell above a column with no band at a level.</summary>
    internal string BandGapClass(ColumnBase<TItem> column) =>
        ColumnClass(column) is { Length: > 0 } own ? "ib-table-band-gap " + own : "ib-table-band-gap";

    internal static RenderFragment BandLabel(ColumnBand band) => band.HeaderTemplate ?? (builder => builder.AddContent(0, band.Title));

    /// <summary>A band's header content: its label, and a grid's menu.</summary>
    internal virtual RenderFragment BandHeader(ColumnBand band, BandSpan span) => BandLabel(band);

    /// <summary>A grid's attributes for a band's header cell, for the script that moves it.</summary>
    internal virtual IReadOnlyDictionary<string, object>? BandAttributes(ColumnBand band, BandSpan span) => null;

    // A table with no columns of its own (a Table's) gets column groups for its bands, which a
    // band's scope="colgroup" names: one before the columns, one per band or column, one after.
    internal RenderFragment PlainBandColGroups => __builder =>
    {
        var before = StartSpan + LeadSpan;
        if (before > 0)
        {
            __builder.OpenElement(0, "colgroup");
            __builder.AddAttribute(1, "span", before);
            __builder.CloseElement();
        }

        foreach (var span in OuterBands())
        {
            __builder.OpenElement(2, "colgroup");
            __builder.AddAttribute(3, "span", span.Count);
            __builder.CloseElement();
        }

        if (LastHeaderCell is not null)
        {
            __builder.OpenElement(4, "colgroup");
            __builder.CloseElement();
        }
    };

    private void PlaceColumn(ColumnBase<TItem> column)
    {
        var nodes = column.Band?.Nodes ?? _nodes;
        if (!nodes.Contains(column))
        {
            nodes.Add(column);
        }
    }

    private void UnplaceColumn(ColumnBase<TItem> column) => (column.Band?.Nodes ?? _nodes).Remove(column);

    // The columns in page order, read through their bands.
    private void OrderColumns()
    {
        if (_bands == 0)
        {
            return;
        }

        var order = new List<ColumnBase<TItem>>(_columns.Count);
        Walk(_nodes);
        // Any the walk didn't reach keep their place at the end.
        order.AddRange(_columns.Where(c => !order.Contains(c)));
        _columns.Clear();
        _columns.AddRange(order);

        void Walk(List<object> nodes)
        {
            foreach (var node in nodes)
            {
                if (node is ColumnBand band)
                {
                    Walk(band.Nodes);
                }
                else if (node is ColumnBase<TItem> column && _columns.Contains(column))
                {
                    order.Add(column);
                }
            }
        }
    }

    // A band's columns join one render after the columns beside it: the view waits for them before
    // it loads, so it knows every sort and filter key the address may name.
    private Task BandsJoined()
    {
        if (_bandsWaiting == 0)
        {
            return Task.CompletedTask;
        }

        _bandsJoined = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return _bandsJoined.Task;
    }

    void IColumnBandHost.AddBand(ColumnBand band)
    {
        var nodes = band.Parent?.Nodes ?? _nodes;
        if (!nodes.Contains(band))
        {
            nodes.Add(band);
        }

        _bands++;
        _bandsWaiting++;
        _bandOrder = null;
    }

    void IColumnBandHost.RemoveBand(ColumnBand band)
    {
        (band.Parent?.Nodes ?? _nodes).Remove(band);
        _bands--;
        _bandOrder = null;
        ColumnChanged();
    }

    void IColumnBandHost.BandChanged()
    {
        if (_columnsReady)
        {
            ColumnChanged();
        }
    }

    void IColumnBandHost.BandReady(ColumnBand band)
    {
        _bandsWaiting--;
        _bandOrder = null;
        OrderColumns();
        ColumnChanged();
        if (!_columnsReady && _bandsWaiting == 0)
        {
            _bandsJoined?.TrySetResult();
        }
    }
}
