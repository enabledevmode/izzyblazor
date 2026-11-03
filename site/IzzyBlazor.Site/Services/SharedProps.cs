namespace IzzyBlazor.Site.Services;

/// <summary>How the shared bases read on the site: the Style props page's sections.</summary>
public static class SharedProps
{
    public static readonly (string Base, string Title, string Label, string Description)[] Sections =
    [
        ("LayoutItemBase", "Item props", "item props", "Item props set how a component sits in its parent, such as its margins, width, growing and shrinking, alignment and column span. Almost every component has them."),
        ("StyledComponentBase", "Surface props", "surface props", "Surface props set how a box looks, such as its padding, background, text color, radius, shadow, borders, height and overflow. Boxes and containers have them, as well as the item props."),
        ("FieldBase", "Field props", "field props", "Field props are the ones every form field shares, such as its label, hint, size, and whether it's required, disabled or read-only. A field also has the item props, which go on its wrapper."),
        ("IzzyComponentBase", "Common props", "common props", "Common props are on every component. They add classes, styles and any other attribute, and hide the component at a breakpoint."),
    ];

    public static string Label(string name) => Sections.FirstOrDefault(s => s.Base == name).Label ?? name;
}
