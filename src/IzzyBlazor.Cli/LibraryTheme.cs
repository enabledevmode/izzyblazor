using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace IzzyBlazor.Cli;

/// <summary>
/// IzzyTheme from a built IzzyBlazor.Core: the app's restored package, or the copy beside this izzy. It's
/// rendered as an app renders it, so what it writes, and the options it takes, are that version's own.
/// </summary>
sealed class LibraryTheme
{
    readonly Type _theme;

    LibraryTheme(Type theme, string version, string source)
    {
        _theme = theme;
        Version = version;
        Source = source;
        Options = [.. theme.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(ParameterAttribute)) && p.Name != "Nonce" && Kind(p.PropertyType) is not null)
            .Select(p => (Kebab(p.Name), p))];
    }

    /// <summary>The library version: the package's, or this izzy's.</summary>
    public string Version { get; }

    /// <summary>"package" (the app's) or "tool" (this izzy's copy), as describe says it.</summary>
    public string Source { get; }

    /// <summary>IzzyTheme's settings, as options (<c>chart-palette</c>), with the parameter each sets.</summary>
    public IReadOnlyList<(string Option, PropertyInfo Parameter)> Options { get; }

    /// <summary>The library in this folder (IzzyBlazor.Core.dll), or null when it can't be loaded on this runtime.</summary>
    public static LibraryTheme? Load(string folder, string source)
    {
        var path = Path.Combine(folder, "IzzyBlazor.Core.dll");
        if (!File.Exists(path)) return null;
        try
        {
            var assembly = new LibraryContext(folder).LoadFromAssemblyPath(path);
            if (assembly.GetType("IzzyBlazor.IzzyTheme") is not { } theme) return null;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
                ?? assembly.GetName().Version!.ToString();
            return new LibraryTheme(theme, version, source);
        }
        catch (Exception e) when (e is BadImageFormatException or FileLoadException or FileNotFoundException or TypeLoadException)
        {
            return null;
        }
    }

    /// <summary>The lib folder of a restored package that this runtime can load: its own .NET version or the nearest below.</summary>
    public static string? LibFolder(string package) =>
        !Directory.Exists(Path.Combine(package, "lib")) ? null
        : Directory.GetDirectories(Path.Combine(package, "lib"))
            .Select(d => (Folder: d, Match: Regex.Match(Path.GetFileName(d), @"^net(\d+)\.(\d+)$")))
            .Where(d => d.Match.Success && int.Parse(d.Match.Groups[1].Value, CultureInfo.InvariantCulture) <= Environment.Version.Major)
            .OrderByDescending(d => int.Parse(d.Match.Groups[1].Value, CultureInfo.InvariantCulture))
            .Select(d => d.Folder).FirstOrDefault();

    /// <summary>An option's value as the parameter takes it, or a usage error naming what it takes.</summary>
    public object Value(string option, PropertyInfo parameter, string value)
    {
        var type = Nullable.GetUnderlyingType(parameter.PropertyType) ?? parameter.PropertyType;
        if (type == typeof(string)) return value;
        if (type == typeof(int))
            return int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? n
                : throw new UsageException($"--{option} is a number. Got \"{value}\".");
        if (type == typeof(bool))
            return bool.TryParse(value, out var b) ? b : throw new UsageException($"--{option} is true or false. Got \"{value}\".");
        var names = Enum.GetNames(type);
        var match = names.FirstOrDefault(n => n.Equals(value.Replace("-", ""), StringComparison.OrdinalIgnoreCase));
        return match is not null ? Enum.Parse(type, match)
            : throw new UsageException($"--{option} is one of: {string.Join(", ", names.Select(Kebab))}. Got \"{value}\".");
    }

    /// <summary>
    /// What IzzyTheme writes for these settings: its meta tags and its style block (null when there's none). An
    /// error the theme gives for a setting is a usage error on its option.
    /// </summary>
    public (List<(string Name, string Content)> Meta, string? Css) Render(Dictionary<string, object?> parameters)
    {
        string html;
        try
        {
            html = RenderAsync(parameters).GetAwaiter().GetResult();
        }
        catch (ArgumentException e) when (Options.FirstOrDefault(o => o.Parameter.Name == e.ParamName) is { Option: not null } bad)
        {
            // "BodyFont is a CSS font-family list…" as "--body-font is a CSS font-family list…".
            var message = Regex.Replace(e.Message, @"\s*\(Parameter '[^']*'\)[\s\S]*$", "");
            throw new UsageException(message.StartsWith(bad.Parameter.Name + " ", StringComparison.Ordinal)
                ? "--" + bad.Option + message[bad.Parameter.Name.Length..]
                : $"--{bad.Option}: {message}");
        }

        var meta = Regex.Matches(html, """<meta name="([^"]*)" content="([^"]*)"\s*/?>""")
            .Select(m => (WebUtility.HtmlDecode(m.Groups[1].Value), WebUtility.HtmlDecode(m.Groups[2].Value))).ToList();
        var style = Regex.Match(html, "<style[^>]*>([\\s\\S]*?)</style>");
        return (meta, style.Success ? style.Groups[1].Value : null);
    }

    async Task<string> RenderAsync(Dictionary<string, object?> parameters)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync(_theme, ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    static string? Kind(Type type) => (Nullable.GetUnderlyingType(type) ?? type) is var t && (t == typeof(string) || t == typeof(int) || t == typeof(bool) || t.IsEnum) ? t.Name : null;

    static string Kebab(string name) => Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", "-").ToLowerInvariant();

    // The library on its own, sharing ASP.NET Core with this izzy (from the shared framework), so its
    // components are the renderer's components.
    sealed class LibraryContext(string folder) : AssemblyLoadContext("IzzyBlazor " + folder)
    {
        protected override Assembly? Load(AssemblyName name) =>
            Path.Combine(folder, name.Name + ".dll") is var path && File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
}
