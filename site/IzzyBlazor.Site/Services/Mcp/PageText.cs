using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.HttpResults;
using IzzyBlazor.Site.Components.Parts;
using IzzyBlazor.Site.Docs;

namespace IzzyBlazor.Site.Services.Mcp;

/// <summary>
/// A docs page as Markdown: an article or a component's Overview, rendered the way the site renders
/// it (static SSR, without the layout) and converted, with each example's preview replaced by its
/// source. Pages don't change while the site runs, so each is rendered once.
/// </summary>
public sealed class PageText(IServiceProvider services, Registry registry, ExampleCatalog catalog)
{
    private static readonly Dictionary<string, Type> Routes = typeof(PageText).Assembly.GetTypes()
        .Where(t => typeof(IComponent).IsAssignableFrom(t))
        .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>().Select(r => (r.Template, Type: t)))
        .Where(r => !r.Template.Contains('{'))
        .ToDictionary(r => r.Template.TrimStart('/'), r => r.Type, StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _pages = new(StringComparer.Ordinal);

    /// <summary>The Markdown of an article (<c>docs/common/forms</c>) or an Overview (<c>docs/components/button</c>), or null if it isn't one.</summary>
    public async Task<string?> OfAsync(string path)
    {
        RazorComponentResult page;
        if (Routes.TryGetValue(path, out var type) && Articles.Sections.Any(s => s.Articles.Any(a => s.HrefOf(a) == path)))
        {
            page = new RazorComponentResult(type);
        }
        else if (registry.Components.FirstOrDefault(c => Registry.OverviewHref(c) == path) is { } component)
        {
            page = new RazorComponentResult<ComponentPage>(new { component.Id });
        }
        else
        {
            return null;
        }

        // Only known pages get here, so the cache holds at most one entry per page. Callers asking
        // for the same page at once share one render; a failed render isn't kept.
        var render = _pages.GetOrAdd(path, _ => new Lazy<Task<string>>(() => RenderAsync(path, page)));
        try
        {
            return await render.Value;
        }
        catch
        {
            _pages.TryRemove(new KeyValuePair<string, Lazy<Task<string>>>(path, render));
            throw;
        }
    }

    private Task<string> RenderAsync(string path, RazorComponentResult page)
    {
        // Without the MCP request's execution context: services that read IHttpContextAccessor
        // (the library's cookie readers) would otherwise see the caller's request, and what they
        // read would be cached for everyone. In a flow of its own, the accessor can point at the
        // made-up request without touching the caller's.
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(async () =>
            {
                await using var scope = services.CreateAsyncScope();
                var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
                context.Request.Method = HttpMethods.Get;
                context.Request.Scheme = "https";
                context.Request.Host = new HostString(CanonicalHost.Host);
                context.Request.Path = "/" + path;
                using var body = new MemoryStream();
                context.Response.Body = body;
                if (scope.ServiceProvider.GetService<IHttpContextAccessor>() is { } accessor)
                {
                    accessor.HttpContext = context;
                }

                await page.ExecuteAsync(context);

                var html = Encoding.UTF8.GetString(body.ToArray());
                var start = html.IndexOf("<article", StringComparison.Ordinal);
                var end = html.LastIndexOf("</article>", StringComparison.Ordinal);
                return Markdown.FromHtml(start >= 0 && end > start ? html[start..end] : html, Example);
            });
        }
    }

    // An example's title and source in place of its preview.
    private string? Example(string id, string title)
    {
        if (catalog.SourceOf(id) is not { } source)
        {
            return null;
        }

        // The notes shown under the preview in a mode ("Static: The browser checks before posting").
        var notes = registry.Example(id)?.Example.Notes.Select(n => $"\n\n{ModeChip.Name(n.Key)}: {n.Value}") ?? [];
        return $"Example: {(title.Length > 0 ? title : id)} (`{id}`)\n\n{Markdown.Fence(source.Source, "razor")}{string.Concat(notes)}";
    }
}
