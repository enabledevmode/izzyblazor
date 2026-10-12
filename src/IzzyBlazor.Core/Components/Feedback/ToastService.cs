using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace IzzyBlazor;

/// <summary>One toast message.</summary>
/// <param name="Message">The text.</param>
/// <param name="Status">What it means; sets the color and icon.</param>
/// <param name="Title">An optional bold line above the text.</param>
/// <param name="Duration">
/// Milliseconds before it closes itself (with JavaScript); 0 keeps it open. Null uses the
/// region's, doubled for a toast with an <paramref name="Action"/>; errors stay until closed.
/// </param>
/// <param name="Action">An optional button on the toast, such as "View" or "Undo".</param>
public sealed record ToastMessage(string Message, Status Status = Status.Info, string? Title = null, int? Duration = null, ToastAction? Action = null);

/// <summary>
/// A button on a toast. Choosing it closes the toast.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="Href"/> makes it a link, which works on any page, static or interactive.</item>
/// <item><see cref="OnClick"/> runs C# on an interactive page. A static page has no connection to
/// run it over, so a toast shown there needs an <see cref="Href"/> (the link is used in its place).</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// Toasts.Success("Invoice sent.", action: new("View", Href: $"/invoices/{id}"));
/// Toasts.Info("Message archived.", action: new("Undo") { OnClick = () => Unarchive(id) });
/// </code>
/// </example>
/// <param name="Label">The button's text.</param>
/// <param name="Href">Where it links to.</param>
public sealed record ToastAction(string Label, string? Href = null)
{
    /// <summary>Runs when chosen, on an interactive page. Not carried over a redirect.</summary>
    [JsonIgnore]
    public Func<Task>? OnClick { get; init; }
}

/// <summary>
/// Carries toasts from a static response that redirects to the page the browser loads next
/// (post-redirect-get). The IzzyBlazor package registers one that uses a short-lived cookie;
/// without one, toasts shown before a redirect are dropped.
/// </summary>
public interface IToastCarrier
{
    /// <summary>Toasts carried over to this request. Called at most once per request.</summary>
    IReadOnlyList<ToastMessage> Receive();

    /// <summary>
    /// Called once when this request first queues a toast. If the response turns out to be a
    /// redirect, <paramref name="queued"/> gives the toasts to carry on.
    /// </summary>
    void CarryIfRedirected(Func<IReadOnlyList<ToastMessage>> queued);
}

/// <summary>
/// Shows toasts in the page's <see cref="ToastRegion"/>, from any render mode. Register it with
/// <c>builder.Services.AddIzzyBlazor()</c> (or <c>AddIzzyBlazorCore()</c> in a WebAssembly
/// project) and inject it where you handle actions.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>On a static page (such as a form post), the toast is rendered in the response. If the
/// response is a redirect, the <see cref="IToastCarrier"/> carries it to the next page, so
/// post-redirect-get works.</item>
/// <item>On an interactive page (Server or WebAssembly), the toast is added in the browser straight away.</item>
/// </list>
/// Show toasts in response to actions rather than in <c>OnInitialized</c>, which runs twice on
/// prerendered interactive pages.
/// </remarks>
public sealed class ToastService : IDisposable
{
    private readonly IJSRuntime? _js;
    private ToastActionInvoker? _actions;
    private readonly IToastCarrier? _carrier;
    private readonly List<ToastMessage> _toasts = [];
    private bool _received;
    private bool _carrying;

    /// <summary>Creates the service. Use <c>AddIzzyBlazor()</c> rather than constructing it.</summary>
    public ToastService(IServiceProvider services)
    {
        _js = services.GetService<IJSRuntime>();
        _carrier = services.GetService<IToastCarrier>();
    }

    private ToastActionInvoker Actions => _actions ??= new ToastActionInvoker();

    /// <inheritdoc />
    public void Dispose() => _actions?.Dispose();

    /// <summary>Raised when a toast is added on a static page, so the region can render it.</summary>
    internal event Action? Changed;

    /// <summary>Toasts to render in this response: those carried over from a redirect, then new ones.</summary>
    internal IReadOnlyList<ToastMessage> Pending
    {
        get
        {
            Receive();
            return _toasts;
        }
    }

    /// <summary>Shows a toast.</summary>
    public void Show(ToastMessage toast)
    {
        ArgumentNullException.ThrowIfNull(toast);
        if (TrySendToBrowser(toast))
        {
            return;
        }

        // Static rendering (or prerendering): render it in this response, or carry it over a redirect.
        if (toast.Action is { Href: null, OnClick: not null })
        {
            throw new InvalidOperationException(
                $"The toast action \"{toast.Action.Label}\" only has OnClick, which can't run on a static page. Give it an Href as well.");
        }

        Receive();
        _toasts.Add(toast);
        if (!_carrying && _carrier is not null)
        {
            _carrying = true;
            _carrier.CarryIfRedirected(() => _toasts);
        }

        Changed?.Invoke();
    }

    /// <summary>Shows a toast.</summary>
    public void Show(string message, Status status = Status.Info, string? title = null, int? duration = null, ToastAction? action = null) =>
        Show(new ToastMessage(message, status, title, duration, action));

    /// <summary>Shows a success toast.</summary>
    public void Success(string message, string? title = null, ToastAction? action = null) => Show(message, Status.Success, title, action: action);

    /// <summary>Shows an information toast.</summary>
    public void Info(string message, string? title = null, ToastAction? action = null) => Show(message, Status.Info, title, action: action);

    /// <summary>Shows a warning toast.</summary>
    public void Warning(string message, string? title = null, ToastAction? action = null) => Show(message, Status.Warning, title, action: action);

    /// <summary>Shows an error toast.</summary>
    public void Error(string message, string? title = null, ToastAction? action = null) => Show(message, Status.Danger, title, action: action);

    // On an interactive page the browser is reachable and the toast goes straight there. During
    // static rendering the JS runtime refuses the call at once, and the toast is queued instead.
    private bool TrySendToBrowser(ToastMessage toast)
    {
        if (_js is null)
        {
            return false;
        }

        ValueTask call;
        try
        {
            call = _js.InvokeVoidAsync("izzyBlazor.showToast", new
            {
                message = toast.Message,
                status = toast.Status.ToString().ToLowerInvariant(),
                title = toast.Title,
                duration = toast.Duration,
                action = toast.Action is { } action
                    ? new
                    {
                        label = action.Label,
                        href = action.Href,
                        id = action.OnClick is null ? null : Actions.Add(action.OnClick),
                        invoker = action.OnClick is null ? null : Actions.Reference,
                    }
                    : null,
            });
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (call.IsCompleted)
        {
            return call.IsCompletedSuccessfully || call.AsTask().Exception?.InnerException is not InvalidOperationException;
        }

        _ = ObserveAsync(call);
        return true;
    }

    private static async Task ObserveAsync(ValueTask call)
    {
        try
        {
            await call;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException or InvalidOperationException)
        {
            // The circuit went away before the toast arrived.
        }
    }

    // Toasts carried over from a redirect come first.
    private void Receive()
    {
        if (_received)
        {
            return;
        }

        _received = true;
        if (_carrier?.Receive() is { Count: > 0 } carried)
        {
            _toasts.InsertRange(0, carried.Where(t => !string.IsNullOrEmpty(t.Message)));
        }
    }
}

/// <summary>Runs toast actions' <see cref="ToastAction.OnClick"/> when the browser asks.</summary>
internal sealed class ToastActionInvoker : IDisposable
{
    // Only the latest few are kept: older toasts have long since closed.
    private const int Keep = 32;
    private readonly Dictionary<string, Func<Task>> _actions = [];
    private readonly Queue<string> _order = new();
    private int _next;

    public ToastActionInvoker() => Reference = DotNetObjectReference.Create(this);

    public DotNetObjectReference<ToastActionInvoker> Reference { get; }

    public string Add(Func<Task> action)
    {
        lock (_actions)
        {
            var id = (++_next).ToString(System.Globalization.CultureInfo.InvariantCulture);
            _actions[id] = action;
            _order.Enqueue(id);
            while (_order.Count > Keep)
            {
                _actions.Remove(_order.Dequeue());
            }

            return id;
        }
    }

    [JSInvokable]
    public Task Run(string id)
    {
        Func<Task>? action;
        lock (_actions)
        {
            _actions.Remove(id, out action);
        }

        return action?.Invoke() ?? Task.CompletedTask;
    }

    public void Dispose() => Reference.Dispose();
}
