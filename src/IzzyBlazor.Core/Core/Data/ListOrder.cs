namespace IzzyBlazor;

/// <summary>
/// What a <c>SortableList</c> posts on a static page: its keys in the order shown, and the move a
/// handle's menu asked for. Bind it with <c>[SupplyParameterFromForm(FormName = "...", Name = "...")]</c>
/// under the list's <c>Name</c>, pass it back to the list as <c>Order</c> so it shows the new order,
/// and save <see cref="Arrange{TItem}"/> when the post is the form's own submit (<see cref="Move"/> is null).
/// </summary>
/// <remarks>
/// Everything here came from the browser. <see cref="Arrange{TItem}"/> only reorders the items it's
/// given: unknown keys are dropped, a repeated key counts once, and items the post left out keep
/// their place after the item they followed.
/// </remarks>
public sealed class ListOrder
{
    /// <summary>The items' keys, in the order the list showed them (after any moves made with script).</summary>
    public string[]? Keys { get; set; }

    /// <summary>In a group of zones, each item's zone, in the same order as <see cref="Keys"/>.</summary>
    public string[]? Zones { get; set; }

    /// <summary>The move a handle's menu posted, such as <c>up:42</c>; null when the form's own submit posted.</summary>
    public string? Move { get; set; }

    /// <summary>The items in the posted order, with the posted move applied.</summary>
    /// <param name="items">The items, in the order they had before the post.</param>
    /// <param name="key">The list's <c>ItemKey</c>.</param>
    public IReadOnlyList<TItem> Arrange<TItem>(IReadOnlyList<TItem> items, Func<TItem, string> key)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(key);
        var entries = ListArrangement.Order(items, key, Keys, zones: null);
        if (ListMove.TryParse(Move, out var move) && move.Kind != ListMoveKind.To)
        {
            ListArrangement.Apply(entries, move);
        }

        return entries.ConvertAll(e => e.Item);
    }
}

/// <summary>A move in a <c>SortableList</c> or between drop zones, before the list changes.</summary>
/// <typeparam name="TItem">The type of the items.</typeparam>
public sealed class ListMoveEventArgs<TItem>
{
    /// <summary>The item that moves.</summary>
    public required TItem Item { get; init; }

    /// <summary>Its key.</summary>
    public required string Key { get; init; }

    /// <summary>Its place before the move, from 0, among the items of its zone (or the list).</summary>
    public required int FromIndex { get; init; }

    /// <summary>Its place after the move, from 0, among the items of the zone it moves to (or the list).</summary>
    public required int ToIndex { get; init; }

    /// <summary>The zone it moves from; null in a <c>SortableList</c>.</summary>
    public string? FromZone { get; init; }

    /// <summary>The zone it moves to; null in a <c>SortableList</c>.</summary>
    public string? ToZone { get; init; }

    /// <summary>Set to keep the item where it was, such as when saving the move failed.</summary>
    public bool Cancel { get; set; }
}

internal enum ListMoveKind
{
    Up,
    Down,
    Top,
    Bottom,
    To,
}

/// <summary>
/// A move as a handle's menu posts it: <c>up:key</c>, <c>down:key</c>, <c>top:key</c>,
/// <c>bottom:key</c> or <c>to:zone:key</c>. The key comes last, so it may hold colons; a zone can't.
/// </summary>
internal readonly record struct ListMove(ListMoveKind Kind, string Key, string? Zone = null)
{
    public static bool TryParse(string? value, out ListMove move)
    {
        move = default;
        var colon = value?.IndexOf(':') ?? -1;
        if (value is null || colon <= 0)
        {
            return false;
        }

        var rest = value[(colon + 1)..];
        switch (value[..colon])
        {
            case "up": move = new(ListMoveKind.Up, rest); break;
            case "down": move = new(ListMoveKind.Down, rest); break;
            case "top": move = new(ListMoveKind.Top, rest); break;
            case "bottom": move = new(ListMoveKind.Bottom, rest); break;
            case "to" when rest.IndexOf(':') is var second and > 0:
                move = new(ListMoveKind.To, rest[(second + 1)..], rest[..second]);
                break;
            default: return false;
        }

        return move.Key.Length > 0;
    }

    public override string ToString() => Kind switch
    {
        ListMoveKind.To => $"to:{Zone}:{Key}",
        _ => $"{Kind.ToString().ToLowerInvariant()}:{Key}",
    };
}

/// <summary>An item with its key and zone (null in a plain list), in display order.</summary>
internal readonly record struct ListEntry<TItem>(TItem Item, string Key, string? Zone);

/// <summary>Where a move took an item: its zone and place before and after.</summary>
internal readonly record struct ListMoved(string Key, string? FromZone, int FromIndex, string? ToZone, int ToIndex);

/// <summary>The ordering and move rules shared by SortableList, DropZoneGroup and <see cref="ListOrder"/>.</summary>
internal static class ListArrangement
{
    /// <summary>
    /// The items in the posted order. Unknown and repeated keys are skipped; an item the post left out
    /// goes after the item it followed before (or first). A posted zone replaces the item's own.
    /// </summary>
    public static List<ListEntry<TItem>> Order<TItem>(
        IReadOnlyList<TItem> items, Func<TItem, string> key, IReadOnlyList<string>? keys, IReadOnlyList<string>? zones,
        Func<TItem, string?>? zoneOf = null)
    {
        var byKey = new Dictionary<string, int>(items.Count, StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            byKey.TryAdd(key(items[i]), i);
        }

        var result = new List<ListEntry<TItem>>(items.Count);
        var placed = new bool[items.Count];
        if (keys is not null)
        {
            for (var p = 0; p < keys.Count; p++)
            {
                if (keys[p] is { } k && byKey.TryGetValue(k, out var i) && !placed[i])
                {
                    placed[i] = true;
                    var zone = zones is not null && p < zones.Count && !string.IsNullOrEmpty(zones[p]) ? zones[p] : zoneOf?.Invoke(items[i]);
                    result.Add(new(items[i], k, zone));
                }
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (placed[i])
            {
                continue;
            }

            var entry = new ListEntry<TItem>(items[i], key(items[i]), zoneOf?.Invoke(items[i]));
            var after = -1;
            for (var j = i - 1; j >= 0 && after < 0; j--)
            {
                if (placed[j])
                {
                    after = result.FindIndex(e => e.Key == key(items[j]));
                }
            }

            result.Insert(after + 1, entry);
            placed[i] = true;
        }

        return result;
    }

    /// <summary>Applies a menu move. Returns null when the item isn't there or the move goes nowhere.</summary>
    public static ListMoved? Apply<TItem>(List<ListEntry<TItem>> entries, ListMove move)
    {
        var at = entries.FindIndex(e => e.Key == move.Key);
        if (at < 0)
        {
            return null;
        }

        var zone = entries[at].Zone;
        var index = IndexInZone(entries, at);
        (string? toZone, int toIndex) = move.Kind switch
        {
            ListMoveKind.Up => (zone, index - 1),
            ListMoveKind.Down => (zone, index + 1),
            ListMoveKind.Top => (zone, 0),
            ListMoveKind.Bottom => (zone, int.MaxValue),
            _ => (move.Zone, int.MaxValue),
        };

        return toIndex < 0 ? null : MoveTo(entries, move.Key, toZone, toIndex);
    }

    /// <summary>
    /// Moves an item to a place among the items of a zone (null: a plain list), clamped to the zone's
    /// ends. Returns null when the item isn't there or would stay where it is.
    /// </summary>
    public static ListMoved? MoveTo<TItem>(List<ListEntry<TItem>> entries, string key, string? toZone, int toIndex)
    {
        var at = entries.FindIndex(e => e.Key == key);
        if (at < 0)
        {
            return null;
        }

        var entry = entries[at];
        var fromIndex = IndexInZone(entries, at);
        entries.RemoveAt(at);

        // Where the zone's items are in the whole list, without the moving one.
        var inZone = new List<int>();
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Zone == toZone)
            {
                inZone.Add(i);
            }
        }

        toIndex = Math.Clamp(toIndex, 0, inZone.Count);
        var insertAt = toIndex < inZone.Count ? inZone[toIndex]
            : inZone.Count > 0 ? inZone[^1] + 1
            : entries.Count;
        entries.Insert(insertAt, entry with { Zone = toZone });

        if (toZone == entry.Zone && toIndex == fromIndex)
        {
            entries.RemoveAt(insertAt);
            entries.Insert(at, entry);
            return null;
        }

        return new(key, entry.Zone, fromIndex, toZone, toIndex);
    }

    /// <summary>The entry's place among the entries of its zone.</summary>
    public static int IndexInZone<TItem>(List<ListEntry<TItem>> entries, int at)
    {
        var index = 0;
        for (var i = 0; i < at; i++)
        {
            if (entries[i].Zone == entries[at].Zone)
            {
                index++;
            }
        }

        return index;
    }
}
