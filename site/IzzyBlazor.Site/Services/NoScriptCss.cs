using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// IzzyBlazor's stylesheet as a browser with JavaScript off applies it, for the docs' Off pages.
/// </summary>
/// <remarks>
/// An Off page renders its examples into the page itself and sends no Blazor and no IzzyBlazor
/// script (App), so they do what the HTML and CSS do on their own. The library's few
/// <c>@media (scripting: ...)</c> rules still follow the browser, which has script on for the
/// site's own: this copy makes <c>(scripting: none)</c> always match and
/// <c>(scripting: enabled)</c> never, so they apply as they would with JavaScript off.
/// </remarks>
public sealed partial class NoScriptCss(IWebHostEnvironment environment, ILogger<NoScriptCss> logger)
{
    /// <summary>Where the library's stylesheet is served.</summary>
    public const string LibraryPath = "_content/IzzyBlazor.Core/izzyblazor.css";

    /// <summary>Where this copy is served: beside the library's, so its relative font URLs resolve.</summary>
    public const string Path = "_content/IzzyBlazor.Core/izzyblazor-noscript.css";

    private readonly Lock _lock = new();
    private (DateTimeOffset Modified, string Css, string Version)? _file;

    /// <summary>
    /// The copy's text and a version from it, made again when the library's file changes (the
    /// asset build's watch mode), or null where the library's file isn't found.
    /// </summary>
    public (string Css, string Version)? File
    {
        get
        {
            var info = environment.WebRootFileProvider.GetFileInfo(LibraryPath);
            if (!info.Exists)
            {
                logger.LogWarning("{Path} wasn't found: Off pages use the library's stylesheet as it is.", LibraryPath);
                return null;
            }

            lock (_lock)
            {
                if (_file is not { } file || file.Modified != info.LastModified)
                {
                    var (css, version) = Read(info);
                    _file = file = (info.LastModified, css, version);
                }

                return (file.Css, file.Version);
            }
        }
    }

    /// <summary>The address to link: the path with the version, so a new build isn't read from the cache.</summary>
    public string Href => File is { } file ? $"{Path}?v={file.Version}" : LibraryPath;

    /// <summary>
    /// Rewrites the scripting media features as JavaScript off reads them: the forms the library
    /// uses, alone or in a comma list (a site test checks none is left in its stylesheet).
    /// </summary>
    public static string Transform(string css) =>
        Scripting().Replace(css, match => match.Groups[1].Value == "none" ? "all" : "not all");

    private static (string, string) Read(IFileInfo info)
    {
        using var reader = new StreamReader(info.CreateReadStream());
        var css = Transform(reader.ReadToEnd());
        var version = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(css)))[..12];
        return (css, version);
    }

    // "(scripting: none)" and "(scripting: enabled)", with any spacing.
    [GeneratedRegex(@"\(\s*scripting\s*:\s*(none|enabled)\s*\)")]
    private static partial Regex Scripting();
}
