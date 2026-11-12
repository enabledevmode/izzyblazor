using System.Globalization;
using System.Text;

namespace IzzyBlazor;

/// <summary>
/// Where one <see cref="Tile"/> sits in a <see cref="TileLayout"/>: its first column and row
/// (from 1) and how many it spans.
/// </summary>
/// <param name="Id">The tile's <see cref="Tile.Id"/>.</param>
/// <param name="Col">Its first column, from 1.</param>
/// <param name="Row">Its first row, from 1.</param>
/// <param name="ColSpan">How many columns it spans.</param>
/// <param name="RowSpan">How many rows it spans.</param>
public sealed record TileState(string Id, int Col, int Row, int ColSpan, int RowSpan);

/// <summary>
/// A <see cref="TileLayout"/>'s layout: each tile's place and size, keyed by its
/// <see cref="Tile.Id"/>. Save it per user and pass it back as <see cref="TileLayout.State"/>.
/// <see cref="ToString"/> and <see cref="Parse"/> give a short string for a cookie or a database
/// column.
/// </summary>
/// <param name="Tiles">Each tile's place and size.</param>
public sealed record TileLayoutState(IReadOnlyList<TileState> Tiles)
{
    /// <summary>The entry for a tile, or null when the state has none for it.</summary>
    public TileState? Find(string id)
    {
        foreach (var tile in Tiles)
        {
            if (tile.Id == id)
            {
                return tile;
            }
        }

        return null;
    }

    /// <summary>
    /// A short string, such as <c>revenue:1,1,8,2;orders:9,1,4,1</c>: each tile's id, then its
    /// column, row, column span and row span. Ids are URL-escaped.
    /// </summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        foreach (var tile in Tiles)
        {
            if (text.Length > 0)
            {
                text.Append(';');
            }

            text.Append(Uri.EscapeDataString(tile.Id)).Append(':')
                .Append(CultureInfo.InvariantCulture, $"{tile.Col},{tile.Row},{tile.ColSpan},{tile.RowSpan}");
        }

        return text.ToString();
    }

    /// <summary>
    /// Reads a string written by <see cref="ToString"/>. Entries that don't read are skipped, so a
    /// cookie from an older layout still gives what it can; null or empty gives null.
    /// </summary>
    public static TileLayoutState? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var tiles = new List<TileState>();
        foreach (var entry in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = entry.LastIndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var numbers = entry[(colon + 1)..].Split(',');
            if (numbers.Length != 4 || !TryReadAll(numbers, out var values))
            {
                continue;
            }

            string id;
            try
            {
                id = Uri.UnescapeDataString(entry[..colon]);
            }
            catch (UriFormatException)
            {
                continue;
            }

            tiles.Add(new TileState(id, values[0], values[1], values[2], values[3]));
        }

        return tiles.Count > 0 ? new TileLayoutState(tiles) : null;
    }

    private static bool TryReadAll(string[] numbers, out int[] values)
    {
        values = new int[numbers.Length];
        for (var i = 0; i < numbers.Length; i++)
        {
            if (!int.TryParse(numbers[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]) || values[i] < 1)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// What an editable <see cref="TileLayout"/> posts on a static page: the layout it showed, and the
/// move its menu asked for. Bind it with <c>[SupplyParameterFromForm(FormName = "...", Name = "...")]</c>
/// under the layout's <c>Name</c> and pass it back as <see cref="TileLayout.Post"/>, so the layout
/// shows the move. Save <see cref="State"/> when the form's own submit posted
/// (<see cref="Move"/> is null).
/// </summary>
/// <remarks>
/// Everything here came from the browser. The layout keeps what it shows inside its grid, but
/// <see cref="State"/> is only parsed: it can name tiles that aren't there, or overlap them.
/// </remarks>
public sealed class TileLayoutPost
{
    /// <summary>The layout as shown, written by <see cref="TileLayoutState.ToString"/>.</summary>
    public string? Tiles { get; set; }

    /// <summary>The move a tile's menu posted, such as <c>right:revenue</c>; null when the form's own submit posted.</summary>
    public string? Move { get; set; }

    /// <summary>The layout as shown, without <see cref="Move"/>; null when none posted.</summary>
    public TileLayoutState? State => TileLayoutState.Parse(Tiles);
}

/// <summary>A move from a tile's menu: by one cell, or one cell bigger or smaller.</summary>
internal enum TileMoveKind
{
    Left,
    Right,
    Up,
    Down,
    Wider,
    Narrower,
    Taller,
    Shorter,
}

/// <summary>A tile's move as posted: <c>right:revenue</c>.</summary>
internal readonly record struct TileMove(TileMoveKind Kind, string Id)
{
    public override string ToString() => $"{Kind.ToString().ToLowerInvariant()}:{Id}";

    public static bool TryParse(string? text, out TileMove move)
    {
        move = default;
        var colon = text?.IndexOf(':') ?? -1;
        if (colon <= 0 || colon == text!.Length - 1 || char.IsDigit(text[0])
            || !Enum.TryParse<TileMoveKind>(text[..colon], ignoreCase: true, out var kind)
            || !Enum.IsDefined(kind))
        {
            return false;
        }

        move = new TileMove(kind, text[(colon + 1)..]);
        return true;
    }
}

/// <summary>
/// What a <see cref="TileLayout"/> shares with its tiles: the tiles in page order, and where each
/// one goes at each column count.
/// </summary>
/// <remarks>
/// The widest column count is the layout as written or saved: tiles with both <c>Col</c> and
/// <c>Row</c> (or an entry in the state) go there, and the rest take the first free cells, row
/// by row, in page order. Each narrower count is worked out from it: spans scale down, and the
/// tiles repack in reading order (by row, then by column), each after the one before, so they
/// wrap onto new rows.
/// </remarks>
internal sealed class TileLayoutContext(TileLayout owner)
{
    private readonly ChildOrder<Tile> _tiles = new();
    private Dictionary<Tile, TilePlace[]>? _places;
    private Dictionary<Tile, TilePlace>? _written;

    public TileLayout Owner { get; } = owner;

    public void Register(Tile tile)
    {
        _tiles.Add(tile);
        _places = null;
        _written = null;
    }

    public void Unregister(Tile tile)
    {
        _tiles.Remove(tile);
        _places = null;
        _written = null;
    }

    /// <summary>The layout is rendering: its tiles will say they're there, in page order.</summary>
    public void BeginPass()
    {
        _tiles.BeginPass();
        _places = null;
        _written = null;
    }

    /// <summary>A tile's parameters were set: places are worked out again on the next read.</summary>
    public void Seen(Tile tile)
    {
        _tiles.Seen(tile);
        _places = null;
        _written = null;
    }

    /// <summary>The tile's place at each of the layout's <see cref="TileLayout.Levels"/>, in the same order.</summary>
    /// <summary>The tile with this id, if any.</summary>
    public Tile? Find(string id) => _tiles.Items.FirstOrDefault(t => t.Id == id);

    /// <summary>
    /// The state changed outside a render pass (a move on an interactive page): places are worked
    /// out again, and every tile draws again, since one whose parameters didn't change wouldn't.
    /// </summary>
    public void Invalidate()
    {
        _places = null;
        _written = null;
        foreach (var tile in _tiles.Items)
        {
            tile.Refresh();
        }
    }

    public TilePlace[] PlacesOf(Tile tile)
    {
        _places ??= Place();
        return _places.TryGetValue(tile, out var places) ? places : [.. Owner.Levels.Select(_ => new TilePlace(1, 1, 1, 1))];
    }

    private Dictionary<Tile, TilePlace[]> Place()
    {
        var levels = Owner.Levels;
        var widest = levels[^1].Columns;
        var written = PlaceWritten(widest);
        _written = written;
        if (Owner.PendingMove is { } move && written.Keys.FirstOrDefault(t => t.Id == move.Id) is { } moved
            && TryMove(moved, move.Kind, out var to))
        {
            written[moved] = to;
            Owner.Moved(moved, move.Kind, to);
        }

        // Reading order of the written layout, which every narrower count keeps.
        var reading = written.OrderBy(p => p.Value.Row).ThenBy(p => p.Value.Col).Select(p => p.Key).ToList();
        var result = written.Keys.ToDictionary(t => t, _ => new TilePlace[levels.Count]);
        for (var i = 0; i < levels.Count; i++)
        {
            var columns = levels[i].Columns;
            if (columns == widest)
            {
                foreach (var (tile, place) in written)
                {
                    result[tile][i] = place;
                }

                continue;
            }

            var taken = new HashSet<(int, int)>();
            var (cursorCol, cursorRow) = (1, 1);
            foreach (var tile in reading)
            {
                var from = written[tile];
                var colSpan = Math.Clamp((int)Math.Ceiling(from.ColSpan * (double)columns / widest), 1, columns);
                var place = NextFree(taken, columns, cursorCol, cursorRow, colSpan, from.RowSpan);
                result[tile][i] = place;
                Take(taken, place);
                (cursorCol, cursorRow) = (place.Col + place.ColSpan, place.Row);
            }
        }

        return result;
    }

    // The layout at the widest count: placed tiles where they say (kept inside the grid), then the
    // rest in the first free cells.
    private Dictionary<Tile, TilePlace> PlaceWritten(int columns)
    {
        var rows = Owner.Rows;
        var taken = new HashSet<(int, int)>();
        var places = new Dictionary<Tile, TilePlace>();
        var sized = new List<(Tile Tile, int? Col, int? Row, int ColSpan, int RowSpan)>();
        foreach (var tile in _tiles.Items)
        {
            var saved = tile.Id is { } id ? Owner.EffectiveState?.Find(id) : null;
            var maxCols = Math.Min(tile.MaxColSpan ?? columns, columns);
            var colSpan = Math.Clamp(saved?.ColSpan ?? tile.ColSpan, Math.Min(tile.MinColSpan, maxCols), maxCols);
            var rowSpan = Math.Clamp(saved?.RowSpan ?? tile.RowSpan, tile.MinRowSpan, Math.Max(tile.MinRowSpan, tile.MaxRowSpan ?? int.MaxValue));
            if (rows > 0)
            {
                rowSpan = Math.Min(rowSpan, rows);
            }

            sized.Add((tile, saved?.Col ?? tile.Col, saved?.Row ?? tile.Row, colSpan, rowSpan));
        }

        foreach (var (tile, col, row, colSpan, rowSpan) in sized)
        {
            if (col is { } c && row is { } r)
            {
                var place = new TilePlace(
                    Math.Clamp(c, 1, columns - colSpan + 1),
                    rows > 0 ? Math.Clamp(r, 1, rows - rowSpan + 1) : r,
                    colSpan,
                    rowSpan);
                places[tile] = place;
                Take(taken, place);
            }
        }

        foreach (var (tile, col, row, colSpan, rowSpan) in sized)
        {
            if (col is null || row is null)
            {
                var place = NextFree(taken, columns, 1, 1, colSpan, rowSpan);
                places[tile] = place;
                Take(taken, place);
            }
        }

        return places;
    }

    // The layout at the widest count. Set by Place before it makes a posted move, which reads it.
    private Dictionary<Tile, TilePlace> Written
    {
        get
        {
            if (_written is null)
            {
                _places = Place();
            }

            return _written!;
        }
    }

    /// <summary>
    /// Where a move from the tile's menu would put it, at the widest count: false when it would
    /// leave the grid, pass the tile's span limits, or (without <c>AllowOverlap</c>) cover another tile.
    /// </summary>
    public bool TryMove(Tile tile, TileMoveKind kind, out TilePlace place)
    {
        var written = Written;
        place = default;
        if (!written.TryGetValue(tile, out var from))
        {
            return false;
        }

        var (col, row, colSpan, rowSpan) = (from.Col, from.Row, from.ColSpan, from.RowSpan);
        switch (kind)
        {
            case TileMoveKind.Left: col--; break;
            case TileMoveKind.Right: col++; break;
            case TileMoveKind.Up: row--; break;
            case TileMoveKind.Down: row++; break;
            case TileMoveKind.Wider: colSpan++; break;
            case TileMoveKind.Narrower: colSpan--; break;
            case TileMoveKind.Taller: rowSpan++; break;
            case TileMoveKind.Shorter: rowSpan--; break;
        }

        var columns = Owner.Levels[^1].Columns;
        var rows = Owner.Rows;
        if (col < 1 || row < 1 || col + colSpan - 1 > columns || (rows > 0 && row + rowSpan - 1 > rows)
            || colSpan < tile.MinColSpan || colSpan > (tile.MaxColSpan ?? columns)
            || rowSpan < tile.MinRowSpan || rowSpan > (tile.MaxRowSpan ?? int.MaxValue))
        {
            return false;
        }

        place = new TilePlace(col, row, colSpan, rowSpan);
        if (!Owner.AllowOverlap)
        {
            foreach (var (other, at) in written)
            {
                if (other != tile && Overlaps(place, at))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>The layout at the widest count, in reading order; with a tile put somewhere else when given.</summary>
    public TileLayoutState CurrentState(Tile? moved = null, TilePlace to = default) =>
        new([.. Written
            .Select(p => (Tile: p.Key, Place: p.Key == moved ? to : p.Value))
            .Where(p => p.Tile.Id is not null)
            .OrderBy(p => p.Place.Row).ThenBy(p => p.Place.Col)
            .Select(p => new TileState(p.Tile.Id!, p.Place.Col, p.Place.Row, p.Place.ColSpan, p.Place.RowSpan))]);

    /// <summary>How many rows the grid shows at the widest count: <c>Rows</c>, or one past the lowest tile.</summary>
    public int GridRows => Owner.Rows > 0 ? Owner.Rows : Written.Values.Select(p => p.Row + p.RowSpan).DefaultIfEmpty(1).Max();

    private static bool Overlaps(TilePlace a, TilePlace b) =>
        a.Col < b.Col + b.ColSpan && b.Col < a.Col + a.ColSpan && a.Row < b.Row + b.RowSpan && b.Row < a.Row + a.RowSpan;

    // The first cells, at or after the given one in row-major order, that the tile fits in.
    private static TilePlace NextFree(HashSet<(int, int)> taken, int columns, int fromCol, int fromRow, int colSpan, int rowSpan)
    {
        for (var row = fromRow; ; row++)
        {
            for (var col = row == fromRow ? fromCol : 1; col <= columns - colSpan + 1; col++)
            {
                if (Fits(taken, col, row, colSpan, rowSpan))
                {
                    return new TilePlace(col, row, colSpan, rowSpan);
                }
            }
        }
    }

    private static bool Fits(HashSet<(int, int)> taken, int col, int row, int colSpan, int rowSpan)
    {
        for (var r = row; r < row + rowSpan; r++)
        {
            for (var c = col; c < col + colSpan; c++)
            {
                if (taken.Contains((c, r)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void Take(HashSet<(int, int)> taken, TilePlace place)
    {
        for (var r = place.Row; r < place.Row + place.RowSpan; r++)
        {
            for (var c = place.Col; c < place.Col + place.ColSpan; c++)
            {
                taken.Add((c, r));
            }
        }
    }
}

/// <summary>Where a tile is drawn at one column count.</summary>
internal readonly record struct TilePlace(int Col, int Row, int ColSpan, int RowSpan);
