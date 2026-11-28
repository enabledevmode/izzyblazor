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
    readonly Type? _sample;
    readonly Assembly _library;

    LibraryTheme(Assembly library, Type theme, string version, string source, string? assets)
    {
        _library = library;
        _theme = theme;
        _sample = library.GetType("IzzyBlazor.ThemeSample");
        Version = version;
        Source = source;
        Assets = assets;
        Options = [.. theme.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(ParameterAttribute)) && p.Name != "Nonce" && Kind(p.PropertyType) is not null)
            .Select(p => (Kebab(p.Name), p))];
    }

    /// <summary>The library version: the package's, or this izzy's.</summary>
    public string Version { get; }

    /// <summary>"package" (the app's) or "tool" (this izzy's copy), as describe says it.</summary>
    public string Source { get; }

    /// <summary>The folder of the library's stylesheets and fonts (izzyblazor.css), or null when it has none.</summary>
    public string? Assets { get; }

    /// <summary>Whether this version has the theme sample (IzzyBlazor 0.1.0-alpha.31 and later).</summary>
    public bool HasSample => _sample is not null;

    /// <summary>IzzyTheme's settings, as options (<c>chart-palette</c>), with the parameter each sets.</summary>
    public IReadOnlyList<(string Option, PropertyInfo Parameter)> Options { get; }

    /// <summary>
    /// The library in this folder (IzzyBlazor.Core.dll), with its stylesheets in <paramref name="assets"/>, or null
    /// when it can't be loaded on this runtime.
    /// </summary>
    public static LibraryTheme? Load(string folder, string source, string? assets)
    {
        var path = Path.Combine(folder, "IzzyBlazor.Core.dll");
        if (!File.Exists(path)) return null;
        try
        {
            var assembly = new LibraryContext(folder).LoadFromAssemblyPath(path);
            if (assembly.GetType("IzzyBlazor.IzzyTheme") is not { } theme) return null;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
                ?? assembly.GetName().Version!.ToString();
            return new LibraryTheme(assembly, theme, version, source,
                assets is not null && File.Exists(Path.Combine(assets, "izzyblazor.css")) ? assets : null);
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
        var html = Settings(() => RenderAsync(_theme, parameters, null).GetAwaiter().GetResult());
        var meta = Regex.Matches(html, """<meta name="([^"]*)" content="([^"]*)"\s*/?>""")
            .Select(m => (WebUtility.HtmlDecode(m.Groups[1].Value), WebUtility.HtmlDecode(m.Groups[2].Value))).ToList();
        var style = Regex.Match(html, "<style[^>]*>([\\s\\S]*?)</style>");
        return (meta, style.Success ? style.Groups[1].Value : null);
    }

    /// <summary>
    /// The theme sample as a page: the library's stylesheet, what IzzyTheme writes for these settings and the
    /// sample, with stylesheet links relative to <paramref name="baseHref"/> (the <see cref="Assets"/> folder, or
    /// where the caller serves it).
    /// </summary>
    public string Sample(Dictionary<string, object?> parameters, string baseHref)
    {
        var head = Settings(() => RenderAsync(_theme, parameters, null).GetAwaiter().GetResult());
        var body = RenderAsync(_sample!, [], _library).GetAwaiter().GetResult();
        // A component's own stylesheet (Chart's chart.css) is linked from _content/IzzyBlazor.Core/, the
        // library's path in an app: here it's beside izzyblazor.css.
        body = body.Replace("\"_content/IzzyBlazor.Core/", "\"", StringComparison.Ordinal);
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <base href="{WebUtility.HtmlEncode(baseHref)}" />
            <title>Theme sample</title>
            <link rel="stylesheet" href="izzyblazor.css" />
            {head}
            </head>
            <body>
            {body}
            </body>
            </html>

            """;
    }

    // An error the theme gives for a setting, as a usage error on its option.
    T Settings<T>(Func<T> render)
    {
        try
        {
            return render();
        }
        catch (ArgumentException e) when (Options.FirstOrDefault(o => o.Parameter.Name == e.ParamName) is { Option: not null } bad)
        {
            // "BodyFont is a CSS font-family list…" as "--body-font is a CSS font-family list…".
            var message = Regex.Replace(e.Message, @"\s*\(Parameter '[^']*'\)[\s\S]*$", "");
            throw new UsageException(message.StartsWith(bad.Parameter.Name + " ", StringComparison.Ordinal)
                ? "--" + bad.Option + message[bad.Parameter.Name.Length..]
                : $"--{bad.Option}: {message}");
        }
    }

    // A component rendered as a static page renders it. With the library given, its services (AddIzzyBlazorCore,
    // found by name, as this izzy doesn't reference it) and what a static page has: an address, no script and
    // a logger for an error boundary.
    static async Task<string> RenderAsync(Type component, Dictionary<string, object?> parameters, Assembly? library)
    {
        var collection = new ServiceCollection();
        if (library is not null)
        {
            library.GetType("Microsoft.Extensions.DependencyInjection.IzzyBlazorCoreServiceCollectionExtensions")
                ?.GetMethod("AddIzzyBlazorCore", [typeof(IServiceCollection)])?.Invoke(null, [collection]);
            collection.AddScoped<NavigationManager, SampleNavigation>();
            collection.AddScoped<Microsoft.JSInterop.IJSRuntime, NoScript>();
            collection.AddScoped<IErrorBoundaryLogger, ErrorLog>();
        }

        await using var services = collection.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync(component, ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    // A static page has no script: a call is a bug in the sample, as it would be on an app's static page.
    sealed class NoScript : Microsoft.JSInterop.IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException($"The theme sample is static: it can't call {identifier}.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    // An error a part of the sample shows in its error boundary goes to standard error.
    sealed class ErrorLog : IErrorBoundaryLogger
    {
        public ValueTask LogErrorAsync(Exception exception)
        {
            Console.Error.WriteLine(exception);
            return ValueTask.CompletedTask;
        }
    }

    // The sample's address: its links are "#", and a table reads its page from the query (none).
    sealed class SampleNavigation : NavigationManager
    {
        public SampleNavigation() => Initialize("https://sample.izzyblazor.invalid/", "https://sample.izzyblazor.invalid/");
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
