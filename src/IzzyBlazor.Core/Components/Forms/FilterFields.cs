using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;

namespace IzzyBlazor;

/// <summary>What a <see cref="FilterForm"/> shares with its fields: their names.</summary>
internal sealed class FilterFormContext
{
    public HashSet<string> Names { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Action? Changed { get; set; }

    public void Register(string name)
    {
        if (Names.Add(name))
        {
            Changed?.Invoke();
        }
    }
}

/// <summary>
/// Shared plumbing for filter fields: each reads its value from the URL, re-reads it when the URL
/// changes, and tells its <see cref="FilterForm"/> its name.
/// </summary>
public abstract class FilterFieldBase : ComponentBase, IDisposable
{
    private string? _id;

    /// <summary>The query-string name, such as <c>status</c>.</summary>
    [Parameter] public string Name { get; set; } = "";

    /// <summary>
    /// The field's name for screen readers (shown beside a <see cref="FilterCheckbox"/>). Filter
    /// bars are compact, so a search box's and a select's isn't shown. Required on
    /// <see cref="FilterSelect"/> and <see cref="FilterCheckbox"/>; <see cref="FilterSearch"/>'s
    /// defaults to "Search".
    /// </summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Whether the field needs a <see cref="Label"/>: the query key is no name for people.</summary>
    private protected virtual bool RequiresLabel => false;

    /// <summary>Control height and text size. Defaults to <see cref="ControlSize.Md"/>.</summary>
    [Parameter] public ControlSize Size { get; set; } = ControlSize.Md;

    /// <summary>Extra attributes for the control.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private IServiceProvider Services { get; set; } = default!;

    /// <summary>The app's <see cref="IzzyText"/>, or English.</summary>
    protected IzzyText TextDefaults => IzzyText.From(Services);

    [CascadingParameter] internal FilterFormContext? Form { get; set; }

    /// <summary>The control's id.</summary>
    protected string ControlId => _id ??= IdGenerator.Next("ib-filter");

    /// <summary>The value in the URL, or null.</summary>
    protected string? CurrentValue { get; private set; }

    /// <summary>The wrapper's classes.</summary>
    protected string FieldClass(string kind) => Size switch
    {
        ControlSize.Sm => $"ib-field ib-field-sm ib-filter-field ib-filter-{kind}",
        ControlSize.Lg => $"ib-field ib-field-lg ib-filter-field ib-filter-{kind}",
        _ => $"ib-field ib-filter-field ib-filter-{kind}",
    };

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException($"{GetType().Name} needs a {nameof(Name)}.");
        }

        Form?.Register(Name);
        Navigation.LocationChanged += OnLocationChanged;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (RequiresLabel && string.IsNullOrWhiteSpace(Label))
        {
            throw new InvalidOperationException($"{GetType().Name} needs a {nameof(Label)}: it names the field for screen readers.");
        }

        Read();
    }

    private void Read()
    {
        var query = QueryString.Of(Navigation.Uri);
        CurrentValue = query.TryGetValue(Name, out var value) ? value.ToString() : null;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => _ = InvokeAsync(() =>
    {
        Read();
        StateHasChanged();
    });

    /// <summary>The visually hidden label.</summary>
    protected RenderFragment HiddenLabel(string text) => builder =>
    {
        builder.OpenElement(0, "label");
        builder.AddAttribute(1, "class", "ib-visually-hidden");
        builder.AddAttribute(2, "for", ControlId);
        builder.AddContent(3, text);
        builder.CloseElement();
    };

    /// <inheritdoc />
    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;
}

/// <summary>A search box for a <see cref="FilterForm"/>. Its value is <c>?q=</c> by default.</summary>
public sealed class FilterSearch : FilterFieldBase
{
    /// <summary>Creates the field with the name <c>q</c>.</summary>
    public FilterSearch() => Name = "q";

    /// <summary>Hint text inside the box. Defaults to "Search".</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", FieldClass("search"));
        builder.AddContent(2, HiddenLabel(Label ?? TextDefaults.Search));
        builder.OpenElement(10, "div");
        builder.AddAttribute(11, "class", "ib-field-control ib-has-start");
        builder.OpenComponent<Icon>(12);
        builder.AddComponentParameter(13, nameof(Icon.Role), IconRole.Search);
        builder.AddComponentParameter(14, nameof(Icon.Class), "ib-field-icon ib-field-icon-start");
        builder.CloseComponent();
        builder.OpenElement(20, "input");
        builder.AddMultipleAttributes(21, AdditionalAttributes);
        builder.AddAttribute(22, "type", "search");
        builder.AddAttribute(23, "id", ControlId);
        builder.AddAttribute(24, "name", Name);
        builder.AddAttribute(25, "value", CurrentValue);
        builder.AddAttribute(26, "placeholder", Placeholder ?? Label ?? TextDefaults.Search);
        builder.AddAttribute(27, "autocomplete", "off");
        builder.AddAttribute(28, "class", "ib-field-input");
        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    }
}

/// <summary>
/// A select for a <see cref="FilterForm"/>, with an "all" option first. Options come from
/// <see cref="Items"/> or an enum (<see cref="EnumType"/>, using [Display(Name)]).
/// </summary>
public sealed class FilterSelect : FilterFieldBase
{
    /// <summary>The values to choose from.</summary>
    [Parameter] public IEnumerable<string>? Items { get; set; }

    /// <summary>An enum whose names are the values.</summary>
    [Parameter] public Type? EnumType { get; set; }

    /// <summary>Display text for a value, when it differs from the value.</summary>
    [Parameter] public Func<string, string>? ItemText { get; set; }

    /// <summary>The first option, meaning no filter. Defaults to "{Label}: All".</summary>
    [Parameter] public string? AllText { get; set; }

    private protected override bool RequiresLabel => true;

    // Each value once: a repeated value would only confuse (and Items plus EnumType can overlap).
    private IEnumerable<(string Value, string Text)> Options() =>
        AllOptions().DistinctBy(o => o.Value, StringComparer.OrdinalIgnoreCase);

    private IEnumerable<(string Value, string Text)> AllOptions()
    {
        if (EnumType is { IsEnum: true } type)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                yield return (field.Name, ItemText?.Invoke(field.Name) ?? field.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? field.Name);
            }
        }

        foreach (var value in Items ?? [])
        {
            yield return (value, ItemText?.Invoke(value) ?? value);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", FieldClass("select"));
        builder.AddContent(2, HiddenLabel(Label!));
        builder.OpenElement(10, "div");
        builder.AddAttribute(11, "class", "ib-field-control ib-has-end");
        builder.OpenElement(12, "select");
        builder.AddMultipleAttributes(13, AdditionalAttributes);
        builder.AddAttribute(14, "id", ControlId);
        builder.AddAttribute(15, "name", Name);
        builder.AddAttribute(16, "class", "ib-field-input ib-field-select");
        builder.AddAttribute(17, "value", CurrentValue ?? "");
        builder.OpenElement(18, "option");
        builder.AddAttribute(19, "value", "");
        builder.AddContent(20, AllText ?? $"{Label}: All");
        builder.CloseElement();
        foreach (var (value, text) in Options())
        {
            builder.OpenElement(30, "option");
            builder.AddAttribute(31, "value", value);
            builder.AddContent(33, text);
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.OpenComponent<Icon>(40);
        builder.AddComponentParameter(41, nameof(Icon.Role), IconRole.ChevronDown);
        builder.AddComponentParameter(42, nameof(Icon.Class), "ib-field-icon ib-field-icon-end");
        builder.CloseComponent();
        builder.CloseElement();
        builder.CloseElement();
    }
}

/// <summary>A checkbox for a <see cref="FilterForm"/>: when checked the URL has <c>name=true</c>.</summary>
public sealed class FilterCheckbox : FilterFieldBase
{
    private protected override bool RequiresLabel => true;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", FieldClass("checkbox"));
        builder.OpenElement(2, "label");
        builder.AddAttribute(3, "class", "ib-check");
        builder.AddAttribute(4, "for", ControlId);
        builder.OpenElement(5, "input");
        builder.AddMultipleAttributes(6, AdditionalAttributes);
        builder.AddAttribute(7, "type", "checkbox");
        builder.AddAttribute(8, "id", ControlId);
        builder.AddAttribute(9, "name", Name);
        builder.AddAttribute(10, "value", "true");
        builder.AddAttribute(11, "class", "ib-check-input");
        builder.AddAttribute(12, "checked", string.Equals(CurrentValue, "true", StringComparison.OrdinalIgnoreCase));
        builder.CloseElement();
        builder.OpenElement(13, "span");
        builder.AddAttribute(14, "class", "ib-check-label");
        builder.AddContent(15, Label);
        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    }
}
