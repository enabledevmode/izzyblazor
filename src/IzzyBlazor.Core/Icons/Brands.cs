// Brand logos as outlines, to sit with the Lucide icons, which no longer include brands: Tabler's brand
// icons (@tabler/icons 3.49.0, MIT; see THIRD-PARTY-NOTICES.md), plus NuGet and .NET, which Tabler
// doesn't have, drawn for IzzyBlazor. All on a 24px grid, drawn at Lucide's 1.5 stroke.
#nullable enable

namespace IzzyBlazor;

/// <summary>
/// The logos sites link to most, by their usual names, as outlines that match <see cref="Lucide"/>.
/// The logos are trademarks of their owners; follow each brand's guidelines.
/// </summary>
/// <example><code>&lt;Icon Name="Brands.GitHub" Label="GitHub" /&gt;</code></example>
public static class Brands
{
    /// <summary>GitHub. From Tabler (<c>brand-github</c>).</summary>
    public static IconData GitHub => new("0 0 24 24", "<path d=\"M9 19c-4.3 1.4 -4.3 -2.5 -6 -3m12 5v-3.5c0 -1 .1 -1.4 -.5 -2c2.8 -.3 5.5 -1.4 5.5 -6a4.6 4.6 0 0 0 -1.3 -3.2a4.2 4.2 0 0 0 -.1 -3.2s-1.1 -.3 -3.5 1.3a12.3 12.3 0 0 0 -6.2 0c-2.4 -1.6 -3.5 -1.3 -3.5 -1.3a4.2 4.2 0 0 0 -.1 3.2a4.6 4.6 0 0 0 -1.3 3.2c0 4.6 2.7 5.7 5.5 6c-.6 .6 -.6 1.2 -.5 2v3.5\"/>", IconStyle.Stroke, 1.5, "github");

    /// <summary>GitLab. From Tabler (<c>brand-gitlab</c>).</summary>
    public static IconData GitLab => new("0 0 24 24", "<path d=\"M21 14l-9 7l-9 -7l3 -11l3 7h6l3 -7l3 11\"/>", IconStyle.Stroke, 1.5, "gitlab");

    /// <summary>YouTube. From Tabler (<c>brand-youtube</c>).</summary>
    public static IconData YouTube => new("0 0 24 24", "<path d=\"M2 8a4 4 0 0 1 4 -4h12a4 4 0 0 1 4 4v8a4 4 0 0 1 -4 4h-12a4 4 0 0 1 -4 -4v-8\"/><path d=\"M10 9l5 3l-5 3l0 -6\"/>", IconStyle.Stroke, 1.5, "youtube");

    /// <summary>X (formerly Twitter). From Tabler (<c>brand-x</c>).</summary>
    public static IconData X => new("0 0 24 24", "<path d=\"M4 4l11.733 16h4.267l-11.733 -16l-4.267 0\"/><path d=\"M4 20l6.768 -6.768m2.46 -2.46l6.772 -6.772\"/>", IconStyle.Stroke, 1.5, "x");

    /// <summary>Bluesky. From Tabler (<c>brand-bluesky</c>).</summary>
    public static IconData Bluesky => new("0 0 24 24", "<path d=\"M6.335 5.144c-1.654 -1.199 -4.335 -2.127 -4.335 .826c0 .59 .35 4.953 .556 5.661c.713 2.463 3.13 2.75 5.444 2.369c-4.045 .665 -4.889 3.208 -2.667 5.41c1.03 1.018 1.913 1.59 2.667 1.59c2 0 3.134 -2.769 3.5 -3.5c.333 -.667 .5 -1.167 .5 -1.5c0 .333 .167 .833 .5 1.5c.366 .731 1.5 3.5 3.5 3.5c.754 0 1.637 -.571 2.667 -1.59c2.222 -2.203 1.378 -4.746 -2.667 -5.41c2.314 .38 4.73 .094 5.444 -2.369c.206 -.708 .556 -5.072 .556 -5.661c0 -2.953 -2.68 -2.025 -4.335 -.826c-2.293 1.662 -4.76 5.048 -5.665 6.856c-.905 -1.808 -3.372 -5.194 -5.665 -6.856\"/>", IconStyle.Stroke, 1.5, "bluesky");

    /// <summary>Mastodon. From Tabler (<c>brand-mastodon</c>).</summary>
    public static IconData Mastodon => new("0 0 24 24", "<path d=\"M18.648 15.254c-1.816 1.763 -6.648 1.626 -6.648 1.626a18.262 18.262 0 0 1 -3.288 -.256c1.127 1.985 4.12 2.81 8.982 2.475c-1.945 2.013 -13.598 5.257 -13.668 -7.636l-.026 -1.154c0 -3.036 .023 -4.115 1.352 -5.633c1.671 -1.91 6.648 -1.666 6.648 -1.666s4.977 -.243 6.648 1.667c1.329 1.518 1.352 2.597 1.352 5.633s-.456 4.074 -1.352 4.944\"/><path d=\"M12 11.204v-2.926c0 -1.258 -.895 -2.278 -2 -2.278s-2 1.02 -2 2.278v4.722m4 -4.722c0 -1.258 .895 -2.278 2 -2.278s2 1.02 2 2.278v4.722\"/>", IconStyle.Stroke, 1.5, "mastodon");

    /// <summary>Discord. From Tabler (<c>brand-discord</c>).</summary>
    public static IconData Discord => new("0 0 24 24", "<path d=\"M8 12a1 1 0 1 0 2 0a1 1 0 0 0 -2 0\"/><path d=\"M14 12a1 1 0 1 0 2 0a1 1 0 0 0 -2 0\"/><path d=\"M15.5 17c0 1 1.5 3 2 3c1.5 0 2.833 -1.667 3.5 -3c.667 -1.667 .5 -5.833 -1.5 -11.5c-1.457 -1.015 -3 -1.34 -4.5 -1.5l-.972 1.923a11.913 11.913 0 0 0 -4.053 0l-.975 -1.923c-1.5 .16 -3.043 .485 -4.5 1.5c-2 5.667 -2.167 9.833 -1.5 11.5c.667 1.333 2 3 3.5 3c.5 0 2 -2 2 -3\"/><path d=\"M7 16.5c3.5 1 6.5 1 10 0\"/>", IconStyle.Stroke, 1.5, "discord");

    /// <summary>Reddit. From Tabler (<c>brand-reddit</c>).</summary>
    public static IconData Reddit => new("0 0 24 24", "<path d=\"M12 8c2.648 0 5.028 .826 6.675 2.14a2.5 2.5 0 0 1 2.326 4.36c0 3.59 -4.03 6.5 -9 6.5c-4.875 0 -8.845 -2.8 -9 -6.294l-1 -.206a2.5 2.5 0 0 1 2.326 -4.36c1.646 -1.313 4.026 -2.14 6.674 -2.14l.999 0\"/><path d=\"M12 8l1 -5l6 1\"/><path d=\"M18 4a1 1 0 1 0 2 0a1 1 0 1 0 -2 0\"/><path d=\"M8.5 13a.5 .5 0 1 0 1 0a.5 .5 0 1 0 -1 0\" fill=\"currentColor\"/><path d=\"M14.5 13a.5 .5 0 1 0 1 0a.5 .5 0 1 0 -1 0\" fill=\"currentColor\"/><path d=\"M10 17c.667 .333 1.333 .5 2 .5s1.333 -.167 2 -.5\"/>", IconStyle.Stroke, 1.5, "reddit");

    /// <summary>Instagram. From Tabler (<c>brand-instagram</c>).</summary>
    public static IconData Instagram => new("0 0 24 24", "<path d=\"M3 7a4 4 0 0 1 4 -4h10a4 4 0 0 1 4 4v10a4 4 0 0 1 -4 4h-10a4 4 0 0 1 -4 -4l0 -10\"/><path d=\"M8.5 12a3.5 3.5 0 1 0 7 0a3.5 3.5 0 0 0 -7 0\"/><path d=\"M17 7v.01\"/>", IconStyle.Stroke, 1.5, "instagram");

    /// <summary>Facebook. From Tabler (<c>brand-facebook</c>).</summary>
    public static IconData Facebook => new("0 0 24 24", "<path d=\"M7 10v4h3v7h4v-7h3l1 -4h-4v-2a1 1 0 0 1 1 -1h3v-4h-3a5 5 0 0 0 -5 5v2h-3\"/>", IconStyle.Stroke, 1.5, "facebook");

    /// <summary>NuGet. Drawn for IzzyBlazor: the dot, the rounded square and its two circles.</summary>
    public static IconData NuGet => new("0 0 24 24", "<circle cx=\"4\" cy=\"4\" r=\"1.5\"/><rect x=\"7\" y=\"7\" width=\"15\" height=\"15\" rx=\"4.5\"/><circle cx=\"11.5\" cy=\"11.5\" r=\"1.25\"/><circle cx=\"16\" cy=\"16\" r=\"2.5\"/>", IconStyle.Stroke, 1.5, "nuget");

    /// <summary>npm. From Tabler (<c>brand-npm</c>).</summary>
    public static IconData Npm => new("0 0 24 24", "<path d=\"M1 8h22v7h-12v2h-4v-2h-6l0 -7\"/><path d=\"M7 8v7\"/><path d=\"M14 8v7\"/><path d=\"M17 11v4\"/><path d=\"M4 11v4\"/><path d=\"M11 11v1\"/><path d=\"M20 11v4\"/>", IconStyle.Stroke, 1.5, "npm");

    /// <summary>.NET. Drawn for IzzyBlazor: the wordmark in strokes.</summary>
    public static IconData DotNet => new("0 0 24 24", "<path d=\"M2.5 17h.01\"/><path d=\"M5.5 17V7l4.5 10V7\"/><path d=\"M16 7h-3.5v10H16\"/><path d=\"M12.5 12h3\"/><path d=\"M18 7h4.5\"/><path d=\"M20.25 7v10\"/>", IconStyle.Stroke, 1.5, "dotnet");
}
