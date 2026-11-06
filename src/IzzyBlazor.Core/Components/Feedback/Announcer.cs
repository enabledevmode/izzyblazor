using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace IzzyBlazor;

/// <summary>
/// Tells screen reader users that something happened, such as "Row deleted." or "Order sent.",
/// from any render mode. For text that describes the page's current state ("12 results"), use a
/// <see cref="LiveRegion"/> instead. Register it with <c>builder.Services.AddIzzyBlazor()</c> (or
/// <c>AddIzzyBlazorCore()</c> in a WebAssembly project) and inject it where you handle actions.
/// </summary>
/// <example>
/// <code>
/// @inject Announcer Announcer
/// ...
/// Announcer.Announce("Row deleted.");
/// Announcer.Announce("Couldn't save: you're offline.", assertive: true);
/// </code>
/// </example>
/// <remarks>
/// <list type="bullet">
/// <item>On an interactive page (Server or WebAssembly), the message is read straight away.</item>
/// <item>On a static page (such as a form post), the message goes in the response and is read when
/// it arrives. That needs an <see cref="AnnouncerRegion"/> in the layout, or a
/// <see cref="ToastRegion"/>, which has one; without either the message is dropped.</item>
/// <item>With JavaScript off, messages aren't read: put the outcome where focus lands (a form's
/// <see cref="FormErrors"/> takes focus) or in the page's heading.</item>
/// </list>
/// Announce in response to actions rather than in <c>OnInitialized</c>, which runs twice on
/// prerendered interactive pages.
/// </remarks>
/// <accessibility>
/// <list type="bullet">
/// <item>Polite by default: read when the screen reader is done with what it's saying. <c>assertive</c> interrupts; keep it for errors that stop the task.</item>
/// <item>Every message is read, the same words twice included, and quick messages don't cut each other off.</item>
/// <item>Messages are read from the open modal dialog when there is one (everything outside it is inert).</item>
/// <item>A message is removed after 7 seconds, so it isn't found later at the end of the page.</item>
/// </list>
/// </accessibility>
/// <seealso cref="LiveRegion"/>
/// <seealso cref="ToastService"/>
public sealed class Announcer
{
    private readonly IJSRuntime? _js;
    private readonly List<Announcement> _pending = [];
    private object? _region;

    /// <summary>Creates the service. Use <c>AddIzzyBlazor()</c> rather than constructing it.</summary>
    public Announcer(IServiceProvider services) => _js = services.GetService<IJSRuntime>();

    /// <summary>Raised when a message is queued on a static page, so the region can render it.</summary>
    internal event Action? Changed;

    /// <summary>Messages to render in this response.</summary>
    internal IReadOnlyList<Announcement> Pending => _pending;

    /// <summary>Tells screen reader users something.</summary>
    /// <param name="text">What's read.</param>
    /// <param name="assertive">Interrupts what the screen reader is saying. Defaults to false (polite).</param>
    public void Announce(string text, bool assertive = false)
    {
        if (string.IsNullOrWhiteSpace(text) || BrowserCall.TrySend(_js, "izzyBlazor.announce", text, assertive))
        {
            return;
        }

        // Static rendering (or prerendering): render it in this response.
        _pending.Add(new Announcement(Guid.NewGuid().ToString("N"), text, assertive));
        Changed?.Invoke();
    }

    /// <summary>
    /// The first region on the page renders the messages; another (an AnnouncerRegion beside a
    /// ToastRegion) renders none, so nothing is read twice.
    /// </summary>
    internal bool Claim(object region)
    {
        _region ??= region;
        return ReferenceEquals(_region, region);
    }

    internal void Release(object region)
    {
        if (ReferenceEquals(_region, region))
        {
            _region = null;
        }
    }
}

/// <summary>A message queued for a static response.</summary>
internal sealed record Announcement(string Id, string Text, bool Assertive);
