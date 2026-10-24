namespace IzzyBlazor.TestHost.Components.Demos;

/// <summary>A demo page: its URL slug, nav title, group and component.</summary>
public sealed record Demo(string Slug, string Title, string Group, Type Component);

/// <summary>
/// Every demo in the host. Adding a component's demo is one line here; it is then
/// served at /static/{slug} and /interactive/{slug} and listed in the nav.
/// </summary>
public static class DemoCatalog
{
    public static IReadOnlyList<Demo> All { get; } =
    [
        new("brand", "Brand color", "Theming", typeof(BrandDemo)),
        new("shell", "AppShell, NavMenu and SiteNav", "Layout", typeof(ShellDemo)),
        new("layouts", "Page layouts", "Layout", typeof(LayoutsDemo)),
        new("box", "Box", "Layout", typeof(BoxDemo)),
        new("stack", "Stack, Row, Column", "Layout", typeof(StackDemo)),
        new("grid", "Grid, Container and more", "Layout", typeof(GridDemo)),
        new("typography", "Text, Heading, List and Prose", "Typography", typeof(TypographyDemo)),
        new("anchor", "Anchor, Code, CodeExample, CopyButton", "Typography", typeof(AnchorDemo)),
        new("buttons", "Button, IconButton", "Actions", typeof(ButtonsDemo)),
        new("toolbar", "Toolbar", "Actions", typeof(ToolbarDemo)),
        new("forms", "Form and fields", "Forms", typeof(FormsDemo)),
        new("disclosure", "Accordion, Collapsible", "Disclosure", typeof(DisclosureDemo)),
        new("overlays", "Menu, Popover, Tooltip, Dialog", "Overlays", typeof(OverlaysDemo)),
        new("tabs", "Tabs", "Navigation", typeof(TabsDemo)),
        new("paging", "Breadcrumb, Pagination, PrevNext", "Navigation", typeof(PagingDemo)),
        new("stepper", "Stepper", "Navigation", typeof(StepperDemo)),
        new("badge", "Badge and Tag", "Feedback", typeof(BadgeDemo)),
        new("alerts", "Alert, Banner and Toast", "Feedback", typeof(AlertDemo)),
        new("loading", "Spinner, Progress, Skeleton", "Feedback", typeof(LoadingDemo)),
        new("avatar", "Avatar", "Data display", typeof(AvatarDemo)),
        new("card", "Card", "Data display", typeof(CardDemo)),
        new("table", "Table", "Data display", typeof(TableDemo)),
        new("display", "Stat, DescriptionList, Timeline, Image, Kbd and more", "Data display", typeof(DisplayDemo)),
        new("icons", "Icon", "Media", typeof(IconsDemo)),
    ];

    public static Demo? Find(string? slug) =>
        All.FirstOrDefault(d => string.Equals(d.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
