namespace IzzyBlazor.Site.Components.Layout;

/// <summary>The part of the site a page is in, for the header's current link and the docs sidebar.</summary>
public enum DocsArea
{
    /// <summary>Not under docs/.</summary>
    None,

    /// <summary>The docs overview and articles: docs/ except docs/components/.</summary>
    Articles,

    /// <summary>The components: docs/components/.</summary>
    Components,
}
