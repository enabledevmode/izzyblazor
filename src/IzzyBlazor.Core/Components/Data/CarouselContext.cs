using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>What a <see cref="Carousel"/> shares with its slides and dots: the slides, in order.</summary>
internal sealed class CarouselContext(Carousel owner)
{
    private readonly List<CarouselItem> _items = [];

    public Carousel Owner { get; } = owner;

    public CarouselDots? Dots { get; set; }

    public int Count => _items.Count;

    public int IndexOf(CarouselItem item) => _items.IndexOf(item);

    public IReadOnlyList<CarouselItem> Items => _items;

    public void Register(CarouselItem item)
    {
        _items.Add(item);

        // A slide added after the first render renumbers the others and adds a dot.
        if (Dots is { Rendered: true })
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
        foreach (var item in _items.ToArray())
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
internal sealed class CarouselDots : ComponentBase, IDisposable
{
    [CascadingParameter] private CarouselContext Context { get; set; } = null!;

    [Inject] private IServiceProvider Services { get; set; } = null!;

    private IzzyText Text => _text ??= IzzyText.From(Services);

    private IzzyText? _text;

    public bool Rendered { get; private set; }

    /// <inheritdoc />
    protected override void OnInitialized() => Context.Dots = this;

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender) => Rendered = true;

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
