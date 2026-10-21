namespace IzzyBlazor.Site.Services;

/// <summary>How the shared bases read on the site: the Style props page's sections.</summary>
public static class SharedProps
{
    public static readonly (string Base, string Title, string Label, string Description)[] Sections =
    [
        ("LayoutItemBase", "Item props", "item props", "How a component sits in its parent: margins, widths, growing and shrinking, alignment and column spans. Almost every component takes them."),
        ("StyledComponentBase", "Surface props", "surface props", "How a box looks: padding, background, text color, radius, shadow, borders, heights and overflow. Boxes and containers take them, on top of the item props."),
        ("FieldBase", "Field props", "field props", "What every form field shares: label, hint, required, disabled, read-only and size, plus the item props on the field's wrapper."),
        ("IzzyComponentBase", "Common props", "common props", "Every component takes these: extra classes and styles, any other attribute, and hiding by breakpoint."),
    ];

    public static string Label(string name) => Sections.FirstOrDefault(s => s.Base == name).Label ?? name;
}
