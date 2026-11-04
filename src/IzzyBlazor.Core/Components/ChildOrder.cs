namespace IzzyBlazor;

/// <summary>
/// The children a parent component keeps a list of (a Splitter's panes, a Carousel's slides), in
/// page order. A child registers once, when it's created; a child added later (an <c>@if</c>
/// between two, a slide inserted in a <c>@foreach</c>) would land at the end. So each time the
/// parent renders, it starts a pass, and the children say they're there as their parameters are
/// set, which Blazor does in page order. The list takes that order before anyone reads it. A child
/// whose parameters didn't change isn't seen; it keeps its place after the child it followed.
/// </summary>
internal sealed class ChildOrder<T> where T : class
{
    private readonly List<T> _items = [];
    private readonly List<T> _seen = [];
    private bool _pass;

    public int Count => Order().Count;

    public void Add(T item) => _items.Add(item);

    public bool Remove(T item)
    {
        _seen.Remove(item);
        return _items.Remove(item);
    }

    /// <summary>The parent is rendering: its children will say they're there, in page order.</summary>
    public void BeginPass()
    {
        _pass = true;
        _seen.Clear();
    }

    /// <summary>A child's parameters were set during the pass.</summary>
    public void Seen(T item)
    {
        if (_pass && !_seen.Contains(item))
        {
            _seen.Add(item);
        }
    }

    public int IndexOf(T item) => Order().IndexOf(item);

    public IReadOnlyList<T> Items => Order();

    private List<T> Order()
    {
        if (!_pass)
        {
            return _items;
        }

        _pass = false;
        if (_seen.Count == 0)
        {
            return _items;
        }

        // The children seen, in page order, with each one not seen put back after the child it
        // followed before.
        var order = new List<T>(_seen);
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (order.Contains(item))
            {
                continue;
            }

            var after = i > 0 ? order.IndexOf(_items[i - 1]) : -1;
            order.Insert(after + 1, item);
        }

        _items.Clear();
        _items.AddRange(order);
        _seen.Clear();
        return _items;
    }
}
