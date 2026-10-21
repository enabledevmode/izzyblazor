namespace IzzyBlazor.Site.Components.Layout;

/// <summary>
/// The docs and demos pages center their columns as one group (sidebar, content and, in the docs,
/// "On this page"), with the header and footer lined up to the same width. Keep in step with
/// site.css (.site-shell).
/// </summary>
public static class SiteWidths
{
    public const double Sidebar = 16;
    public const double Main = 58;
    public const double Aside = 15;

    /// <summary>The docs: sidebar, content and outline, in rem.</summary>
    public const double Docs = Sidebar + Main + Aside;

    /// <summary>The demos: sidebar and content, the content taking the outline's room. In rem.</summary>
    public const double Demos = Docs;
}
