using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>Where a <see cref="Carousel"/>'s dots go.</summary>
public enum CarouselDots
{
    /// <summary>On the slide when one shows at a time; below when several do.</summary>
    Auto,

    /// <summary>On the slide, at the bottom.</summary>
    Over,

    /// <summary>Below the slides, across the carousel's width.</summary>
    Below,

    /// <summary>No dots.</summary>
    None,
}

/// <summary>How a <see cref="Carousel"/>'s slides give way to each other.</summary>
public enum CarouselTransition
{
    /// <summary>The slides sit in a row that scrolls; several can show at once.</summary>
    Slide,

    /// <summary>One slide shows at a time, and the next fades in over it.</summary>
    Fade,
}

/// <summary>What a <see cref="Carousel"/> shares with its slides and dots: the slides, in order.</summary>
internal sealed class CarouselContext(Carousel owner)
{
    private readonly ChildOrder<CarouselItem> _items = new();

    public Carousel Owner { get; } = owner;

    public CarouselDotList? Dots { get; set; }

    public int Count => _items.Count;

    public int IndexOf(CarouselItem item) => _items.IndexOf(item);

    public IReadOnlyList<CarouselItem> Items => _items.Items;

    /// <summary>Set once the carousel has rendered on an interactive page.</summary>
    public bool Rendered { get; set; }

    public void BeginPass() => _items.BeginPass();

    public void Seen(CarouselItem item) => _items.Seen(item);

    public void Register(CarouselItem item)
    {
        _items.Add(item);

        // A slide added after the first render renumbers the others and adds a dot.
        if (Rendered)
        {
            Refresh();
        }
    }

    public void Unregister(CarouselItem item)
    {
        if (_items.Remove(item))
        {
            Refresh();
        }
    }

    /// <summary>Draws the slides' names and the dots again.</summary>
    public void Refresh()
    {
        foreach (var item in _items.Items.ToArray())
        {
            item.Refresh();
        }

        Dots?.Refresh();
    }
}

/// <summary>
/// A <see cref="Carousel"/>'s dots: a button per slide, rendered after the slides so it knows how
/// many there are. With several slides in view, the script hides the dots past the last page.
/// </summary>
internal sealed class CarouselDotList : ComponentBase, IDisposable
{
    [CascadingParameter] private CarouselContext Context { get; set; } = null!;

    [Inject] private IServiceProvider Services { get; set; } = null!;

    private IzzyText Text => _text ??= IzzyText.From(Services);

    private IzzyText? _text;

    /// <inheritdoc />
    protected override void OnInitialized() => Context.Dots = this;

    internal void Refresh() => StateHasChanged();

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var current = Context.Owner.StartDot;
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ib-carousel-dots");
        builder.AddAttribute(2, "data-ib-page-label", Text.SlidesRange);
        builder.AddAttribute(3, "hidden", Context.Count < 2);
        for (var i = 0; i < Context.Count; i++)
        {
            builder.OpenElement(4, "button");
            builder.SetKey(i);
            builder.AddAttribute(5, "type", "button");
            builder.AddAttribute(6, "class", "ib-carousel-dot");
            builder.AddAttribute(7, "aria-label", string.Format(Text.GoToSlide, i + 1));
            builder.AddAttribute(8, "aria-controls", Context.Owner.TrackId);
            builder.AddAttribute(9, "aria-current", i == current ? "true" : null);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Context.Dots == this)
        {
            Context.Dots = null;
        }
    }
}
