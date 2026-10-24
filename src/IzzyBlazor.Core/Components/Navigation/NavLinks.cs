namespace IzzyBlazor;

/// <summary>
/// What a list of nav links (<see cref="NavMenu"/>, <see cref="SiteNav"/>, the copy of a SiteNav's
/// links in an <see cref="AppSidebar"/>) shares with its <see cref="NavItem"/>s, so that of links to
/// one page told apart by the query (<c>?view=inbox</c>), only one is current: a link without a
/// query matches its page whatever the query, so when a link with one matches, the one without
/// gives way, as in <see cref="Tabs"/>.
/// </summary>
internal sealed class NavLinks
{
    private readonly List<NavItem> _items = [];

    /// <summary>Raised when an item joins or leaves, so items that already rendered check again.</summary>
    public event Action? Changed;

    /// <summary>
    /// Whether an item whose link has a query string, to the page at <paramref name="path"/>, is
    /// current at <paramref name="uri"/>.
    /// </summary>
    public bool QueriedLinkIsCurrent(string uri, string path) =>
        _items.Any(item => item.HasQuery && item.Path == path && item.IsActive(uri));

    public void Add(NavItem item)
    {
        if (!_items.Contains(item))
        {
            _items.Add(item);
            Changed?.Invoke();
        }
    }

    public void Remove(NavItem item)
    {
        if (_items.Remove(item))
        {
            Changed?.Invoke();
        }
    }
}
