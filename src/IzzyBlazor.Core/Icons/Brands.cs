// Brand logos as outlines, to sit with the Hugeicons outline icons: Hugeicons' own logos (MIT; see
// THIRD-PARTY-NOTICES.md), plus NuGet and .NET, which Hugeicons doesn't have, drawn for IzzyBlazor
// on the same 24px grid and 1.5px stroke.
#nullable enable

namespace IzzyBlazor;

/// <summary>
/// The logos sites link to most, by their usual names, as outlines that match <see cref="Hugeicons"/>.
/// Hugeicons has more, such as <c>Hugeicons.Linkedin01</c> and <c>Hugeicons.Twitch</c>. The logos are
/// trademarks of their owners; follow each brand's guidelines.
/// </summary>
/// <example><code>&lt;Icon Name="Brands.GitHub" Label="GitHub" /&gt;</code></example>
public static class Brands
{
    /// <summary>GitHub. <see cref="Hugeicons.Github"/>.</summary>
    public static IconData GitHub => Hugeicons.Github;

    /// <summary>GitLab. <see cref="Hugeicons.Gitlab"/>.</summary>
    public static IconData GitLab => Hugeicons.Gitlab;

    /// <summary>YouTube. <see cref="Hugeicons.Youtube"/>.</summary>
    public static IconData YouTube => Hugeicons.Youtube;

    /// <summary>X (formerly Twitter). <see cref="Hugeicons.NewTwitter"/>.</summary>
    public static IconData X => Hugeicons.NewTwitter;

    /// <summary>Bluesky. <see cref="Hugeicons.Bluesky"/>.</summary>
    public static IconData Bluesky => Hugeicons.Bluesky;

    /// <summary>Mastodon. <see cref="Hugeicons.Mastodon"/>.</summary>
    public static IconData Mastodon => Hugeicons.Mastodon;

    /// <summary>Discord. <see cref="Hugeicons.Discord"/>.</summary>
    public static IconData Discord => Hugeicons.Discord;

    /// <summary>Reddit. <see cref="Hugeicons.Reddit"/>.</summary>
    public static IconData Reddit => Hugeicons.Reddit;

    /// <summary>Instagram. <see cref="Hugeicons.Instagram"/>.</summary>
    public static IconData Instagram => Hugeicons.Instagram;

    /// <summary>Facebook. <see cref="Hugeicons.Facebook02"/>.</summary>
    public static IconData Facebook => Hugeicons.Facebook02;

    /// <summary>NuGet. Drawn for IzzyBlazor: the dot, the rounded square and its two circles.</summary>
    public static IconData NuGet => new("0 0 24 24", "<circle cx=\"4\" cy=\"4\" r=\"1.5\"/><rect x=\"7\" y=\"7\" width=\"15\" height=\"15\" rx=\"4.5\"/><circle cx=\"11.5\" cy=\"11.5\" r=\"1.25\"/><circle cx=\"16\" cy=\"16\" r=\"2.5\"/>", IconStyle.Stroke, 1.5, "nuget");

    /// <summary>npm. <see cref="Hugeicons.Npm"/>.</summary>
    public static IconData Npm => Hugeicons.Npm;

    /// <summary>.NET. Drawn for IzzyBlazor: the wordmark in strokes.</summary>
    public static IconData DotNet => new("0 0 24 24", "<path d=\"M2.5 17h.01\"/><path d=\"M5.5 17V7l4.5 10V7\"/><path d=\"M16 7h-3.5v10H16\"/><path d=\"M12.5 12h3\"/><path d=\"M18 7h4.5\"/><path d=\"M20.25 7v10\"/>", IconStyle.Stroke, 1.5, "dotnet");
}
