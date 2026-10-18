namespace IzzyBlazor;

/// <summary>What <see cref="Tabs"/> shares with its <see cref="Tab"/>s.</summary>
internal sealed class TabsContext(Tabs owner)
{
    private readonly List<Tab> _tabs = [];
    private readonly List<TabLink> _links = [];

    public IReadOnlyList<Tab> Items => _tabs;

    /// <summary>Link tabs, which turn the Tabs into a nav of links.</summary>
    public IReadOnlyList<TabLink> Links => _links;

    public string RootId => owner.RootId;

    /// <summary>The padding inside every panel, unless a tab sets its own.</summary>
    public Responsive<int> ContentPadding => owner.ContentPadding;

    public bool IsSelected(Tab tab) => owner.SelectedValue == KeyOf(tab);

    /// <summary>
    /// The tab's value, made unique: a second tab with the same value (or a title that slugs the
    /// same) gets "-2", and so on, so ids and selection never collide.
    /// </summary>
    public string KeyOf(Tab tab)
    {
        var key = tab.Key;
        var n = 0;
        foreach (var other in _tabs)
        {
            if (other.Key == key)
            {
                n++;
            }

            if (ReferenceEquals(other, tab))
            {
                break;
            }
        }

        return n <= 1 ? key : $"{key}-{n}";
    }

    /// <summary>The key as an id fragment: anything but letters, digits, "-" and "_" becomes "-".</summary>
    private string IdPart(Tab tab)
    {
        var key = KeyOf(tab);
        return string.Create(key.Length, key, (span, k) =>
        {
            for (var i = 0; i < k.Length; i++)
            {
                span[i] = char.IsLetterOrDigit(k[i]) || k[i] is '-' or '_' ? k[i] : '-';
            }
        });
    }

    public void Changed() => owner.TabsChanged();

    public void FieldsChanged() => owner.FieldsChanged();

    public void Register(Tab tab)
    {
        if (!_tabs.Contains(tab))
        {
            _tabs.Add(tab);
            owner.TabsChanged();
        }
    }

    public void Unregister(Tab tab)
    {
        if (_tabs.Remove(tab))
        {
            owner.TabsChanged();
        }
    }

    public void RegisterLink(TabLink link)
    {
        if (!_links.Contains(link))
        {
            _links.Add(link);
            owner.TabsChanged();
        }
    }

    public void UnregisterLink(TabLink link)
    {
        if (_links.Remove(link))
        {
            owner.TabsChanged();
        }
    }

    public string InputId(Tab tab) => $"{RootId}-{IdPart(tab)}";

    public string TabId(Tab tab) => $"{RootId}-{IdPart(tab)}-tab";

    public string PanelId(Tab tab) => $"{RootId}-{IdPart(tab)}-panel";
}
