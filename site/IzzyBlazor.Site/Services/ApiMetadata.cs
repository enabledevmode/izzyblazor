using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using IzzyBlazor.DocsGen;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// The API metadata DocsGen wrote at build time (embedded as izzyblazor.api.json): components,
/// shared bases and types, with their doc comments as HTML.
/// </summary>
public sealed partial class ApiMetadata
{
    private readonly Dictionary<string, ApiComponent> _components;
    private readonly Dictionary<string, ApiType> _types;
    private readonly Dictionary<string, ApiBase> _bases;

    private ApiMetadata(string json, ApiFile file)
    {
        Json = json;
        File = file;
        _components = file.Components.ToDictionary(c => c.Name, StringComparer.Ordinal);
        _types = file.Types.ToDictionary(t => t.Name, StringComparer.Ordinal);
        _bases = file.Bases.ToDictionary(b => b.Name, StringComparer.Ordinal);
    }

    public string Json { get; }

    public ApiFile File { get; }

    public string Version => File.Version;

    /// <summary>
    /// The API page link for a component or one of its members (a component name and an optional
    /// member name), or null. The registry knows which page documents what, so it sets this.
    /// </summary>
    public Func<string, string?, string?>? ApiHref { get; set; }

    public static ApiMetadata Load()
    {
        using var stream = typeof(ApiMetadata).Assembly.GetManifestResourceStream("izzyblazor.api.json")
            ?? throw new InvalidOperationException("izzyblazor.api.json isn't embedded. Build the site project, which runs DocsGen.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return new ApiMetadata(json, JsonSerializer.Deserialize<ApiFile>(json, ApiFile.JsonOptions)!);
    }

    public ApiComponent? Component(string name) => _components.GetValueOrDefault(name);

    public ApiType? Type(string name) => _types.GetValueOrDefault(name);

    public ApiBase? Base(string name) => _bases.GetValueOrDefault(name);

    /// <summary>
    /// Turns the generator's <c>cref:</c> links into pages: a member of the component this page is
    /// about goes to its anchor here; a member of another component, to that component's API page;
    /// a type this page lists, to its anchor. Anything without a page keeps its text.
    /// </summary>
    public string Resolve(string? html, ApiPageContext context)
    {
        if (string.IsNullOrEmpty(html))
        {
            return "";
        }

        return CrefLink().Replace(html, match =>
        {
            var cref = WebUtility.HtmlDecode(match.Groups["cref"].Value);
            var inner = match.Groups["inner"].Value;
            var href = HrefFor(cref, context);
            return href is null ? inner : $"<a href=\"{WebUtility.HtmlEncode(href)}\">{inner}</a>";
        });
    }

    private string? HrefFor(string cref, ApiPageContext context)
    {
        if (cref.Length < 3 || cref[1] != ':')
        {
            return null;
        }

        var kind = cref[0];
        var path = GenericArity().Replace(cref[2..], "");
        var paren = path.IndexOf('(');
        if (paren >= 0)
        {
            path = path[..paren];
        }

        var parts = path.Split('.');

        // A cref the compiler bound to a private member named like a type (a <see cref="Dialog"/>
        // inside a class with a Dialog property) means the type.
        if (kind is 'P' or 'M' or 'E' && parts.Length >= 2 && !Documented(parts[^2], parts[^1], context)
            && (_components.ContainsKey(parts[^1]) || _types.ContainsKey(parts[^1])))
        {
            kind = 'T';
            parts = [parts[^1]];
        }

        if (kind == 'T')
        {
            var name = parts[^1];
            if (_components.ContainsKey(name))
            {
                return ApiHref?.Invoke(name, null);
            }

            if (context.Types.Contains(name))
            {
                return $"{context.Page}#type-{Anchor(name)}";
            }

            return _bases.ContainsKey(name) ? $"docs/common/style-props#{Anchor(name)}" : null;
        }

        if (parts.Length < 2)
        {
            return null;
        }

        var owner = parts[^2];
        var member = parts[^1];

        if (kind == 'F' && context.Types.Contains(owner))
        {
            return $"{context.Page}#type-{Anchor(owner)}";
        }

        if (kind != 'F' && !Documented(owner, member, context))
        {
            return null;
        }

        if (owner == context.Component || context.DeclaredOn.Contains(owner))
        {
            return $"{context.Page}#{Anchor(member)}";
        }

        if (_components.ContainsKey(owner))
        {
            return ApiHref?.Invoke(owner, member);
        }

        if (_bases.ContainsKey(owner))
        {
            return $"docs/common/style-props#{Anchor(owner)}-{Anchor(member)}";
        }

        return context.Types.Contains(owner) ? $"{context.Page}#type-{Anchor(owner)}" : null;
    }

    // Whether the member has an anchor somewhere: it's a listed parameter, event or method.
    private bool Documented(string owner, string member, ApiPageContext context)
    {
        static bool Lists(IEnumerable<ApiMember> members, string name) => members.Any(m => m.Name == name);

        if ((owner == context.Component || context.DeclaredOn.Contains(owner)) && context.Component is { } name
            && _components.TryGetValue(name, out var current))
        {
            return Lists(current.Properties.Concat(current.Events).Concat(current.Methods), member);
        }

        if (_components.TryGetValue(owner, out var component))
        {
            return Lists(component.Properties.Concat(component.Events).Concat(component.Methods), member);
        }

        if (_bases.TryGetValue(owner, out var shared))
        {
            return Lists(shared.Properties, member);
        }

        return _types.TryGetValue(owner, out var type) && Lists(type.Members, member);
    }

    /// <summary>The context for a page about a component: its own members, and the types it lists.</summary>
    public static ApiPageContext ContextFor(string page, ApiComponent component, IEnumerable<string>? types = null) => new(
        page,
        component.Name,
        component.Properties.Concat(component.Events).Concat(component.Methods).Select(m => m.DeclaredOn).OfType<string>()
            .Select(d => GenericArity().Replace(d.Split('<')[0], "")).ToHashSet(),
        (types ?? component.Types).ToHashSet());

    /// <summary>An element id for a member or type name: <c>FullWidth</c> becomes <c>fullwidth</c>.</summary>
    public static string Anchor(string name) => name.ToLowerInvariant();

    /// <summary>The text of a doc comment's HTML, for places that can't show markup (a card's line, a meta tag).</summary>
    public static string PlainText(string? html) =>
        html is null ? "" : WebUtility.HtmlDecode(Tags().Replace(html, "")).Trim();

    [GeneratedRegex("<a href=\"cref:(?<cref>[^\"]*)\">(?<inner>.*?)</a>")]
    private static partial Regex CrefLink();

    [GeneratedRegex(@"`+\d+")]
    private static partial Regex GenericArity();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}

/// <summary>Where a doc comment is shown, for resolving its links.</summary>
/// <param name="Page">The page's path, such as <c>docs/components/button/api</c>.</param>
/// <param name="Component">The component the page is about.</param>
/// <param name="DeclaredOn">Bases whose members the page lists as the component's own (<c>ButtonBase</c>).</param>
/// <param name="Types">Types the page has a section for.</param>
public sealed record ApiPageContext(string Page, string? Component, IReadOnlySet<string> DeclaredOn, IReadOnlySet<string> Types);
