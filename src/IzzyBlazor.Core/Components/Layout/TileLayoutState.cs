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
/// What a <see cref="TileLayout"/> shares with its tiles: the tiles in page order, and where each
/// one goes. Tiles with both <c>Col</c> and <c>Row</c> (or an entry in the state) go there; the
/// rest take the first free cells, row by row, in page order.
/// </summary>
internal sealed class TileLayoutContext(TileLayout owner)
{
    private readonly ChildOrder<Tile> _tiles = new();
    private Dictionary<Tile, TilePlace>? _places;

    public TileLayout Owner { get; } = owner;

    public void Register(Tile tile)
    {
        _tiles.Add(tile);
        _places = null;
    }

    public void Unregister(Tile tile)
    {
        _tiles.Remove(tile);
        _places = null;
    }

    /// <summary>The layout is rendering: its tiles will say they're there, in page order.</summary>
    public void BeginPass()
    {
        _tiles.BeginPass();
        _places = null;
    }

    /// <summary>A tile's parameters were set: places are worked out again on the next read.</summary>
    public void Seen(Tile tile)
    {
        _tiles.Seen(tile);
        _places = null;
    }

    public TilePlace PlaceOf(Tile tile)
    {
        _places ??= Place();
        return _places.TryGetValue(tile, out var place) ? place : new TilePlace(1, 1, 1, 1, 0);
    }

    private Dictionary<Tile, TilePlace> Place()
    {
        var columns = Owner.Columns;
        var rows = Owner.Rows;
        var tiles = _tiles.Items;
        var taken = new HashSet<(int Col, int Row)>();
        var places = new Dictionary<Tile, (int Col, int Row, int ColSpan, int RowSpan)>();

        // Spans clamped to the grid; then tiles with a place, then the rest in the first free cells.
        var sized = new List<(Tile Tile, int? Col, int? Row, int ColSpan, int RowSpan)>(tiles.Count);
        foreach (var tile in tiles)
        {
            var saved = tile.Id is { } id ? Owner.State?.Find(id) : null;
            var colSpan = Math.Clamp(saved?.ColSpan ?? tile.ColSpan, 1, columns);
            var rowSpan = Math.Max(1, saved?.RowSpan ?? tile.RowSpan);
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
                var place = (Math.Clamp(c, 1, columns - colSpan + 1), rows > 0 ? Math.Clamp(r, 1, rows - rowSpan + 1) : Math.Max(1, r), colSpan, rowSpan);
                places[tile] = place;
                Take(taken, place);
            }
        }

        foreach (var (tile, col, row, colSpan, rowSpan) in sized)
        {
            if (col is null || row is null)
            {
                var place = FirstFree(taken, columns, colSpan, rowSpan);
                places[tile] = place;
                Take(taken, place);
            }
        }

        // Reading order, for the one column on small screens: by row, then by column.
        var order = places.OrderBy(p => p.Value.Row).ThenBy(p => p.Value.Col).Select(p => p.Key).ToList();
        return places.ToDictionary(
            p => p.Key,
            p => new TilePlace(p.Value.Col, p.Value.Row, p.Value.ColSpan, p.Value.RowSpan, order.IndexOf(p.Key)));
    }

    private static (int Col, int Row, int ColSpan, int RowSpan) FirstFree(HashSet<(int, int)> taken, int columns, int colSpan, int rowSpan)
    {
        for (var row = 1; ; row++)
        {
            for (var col = 1; col <= columns - colSpan + 1; col++)
            {
                if (Fits(taken, col, row, colSpan, rowSpan))
                {
                    return (col, row, colSpan, rowSpan);
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

    private static void Take(HashSet<(int, int)> taken, (int Col, int Row, int ColSpan, int RowSpan) place)
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

/// <summary>Where a tile is drawn, and its place in reading order.</summary>
internal readonly record struct TilePlace(int Col, int Row, int ColSpan, int RowSpan, int Order);
