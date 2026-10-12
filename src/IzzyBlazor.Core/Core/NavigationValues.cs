namespace IzzyBlazor;

/// <summary>How <see cref="Tabs"/> draw their tab list.</summary>
public enum TabsVariant
{
    /// <summary>A line under the list, with the selected tab underlined (the default).</summary>
    Line,

    /// <summary>The selected tab gets a soft accent background.</summary>
    Soft,

    /// <summary>A segmented control: the tabs sit in a tray and the selected one is raised.</summary>
    Segmented,
}

internal static class NavigationNames
{
    public static string Name(this TabsVariant variant) => variant switch
    {
        TabsVariant.Line => "line",
        TabsVariant.Soft => "soft",
        TabsVariant.Segmented => "segmented",
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };
}

/// <summary>What goes between <see cref="Breadcrumb"/> items.</summary>
public enum BreadcrumbSeparator
{
    /// <summary>A small chevron (the default). Mirrors in right-to-left languages.</summary>
    Chevron,

    /// <summary>A slash.</summary>
    Slash,
}
