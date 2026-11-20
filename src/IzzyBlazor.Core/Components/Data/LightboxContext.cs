namespace IzzyBlazor;

/// <summary>What a <see cref="Lightbox"/> shows beside the picture to move between its items.</summary>
public enum LightboxNav
{
    /// <summary>Nothing more than the previous and next buttons.</summary>
    None,

    /// <summary>A dot for each item, under the picture.</summary>
    Dots,

    /// <summary>A small picture of each item, under the picture.</summary>
    Thumbnails,
}

/// <summary>What a <see cref="Lightbox"/> shares with its items: the items, in order.</summary>
internal sealed class LightboxContext(Lightbox owner)
{
    private readonly ChildOrder<LightboxItem> _items = new();

    public IReadOnlyList<LightboxItem> Items => _items.Items;

    public void BeginPass() => _items.BeginPass();

    public void Seen(LightboxItem item) => _items.Seen(item);

    // The viewer is drawn from the items, which arrive after the lightbox's own render.
    public void Register(LightboxItem item)
    {
        _items.Add(item);
        owner.Refresh();
    }

    public void Unregister(LightboxItem item)
    {
        if (_items.Remove(item))
        {
            owner.Refresh();
        }
    }

    public void Changed() => owner.Refresh();
}
