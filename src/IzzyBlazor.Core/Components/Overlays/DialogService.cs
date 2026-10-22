using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>How a dialog opened by <see cref="DialogService"/> ended.</summary>
/// <param name="Canceled">True when it was canceled: Escape, the backdrop, the close button, or <see cref="DialogInstance.Cancel"/>.</param>
/// <param name="Data">What the dialog closed with, from <see cref="DialogInstance.Close"/> or <see cref="DialogService.Close"/>.</param>
public sealed record DialogResult(bool Canceled, object? Data)
{
    /// <summary>A result with data.</summary>
    public static DialogResult Ok(object? data = null) => new(false, data);

    /// <summary>A canceled result.</summary>
    public static DialogResult Cancel() => new(true, null);

    /// <summary>The data as <typeparamref name="T"/>, or the default if it's missing or another type.</summary>
    public T? DataAs<T>() => Data is T value ? value : default;
}

/// <summary>How <see cref="DialogService.OpenAsync{TComponent}"/> shows a dialog.</summary>
public sealed class DialogOptions
{
    /// <summary>The width. Defaults to <see cref="DialogSize.Md"/>.</summary>
    public DialogSize Size { get; init; } = DialogSize.Md;

    /// <summary>Center (the default), or a drawer or sheet at an edge.</summary>
    public DialogPlacement Placement { get; init; } = DialogPlacement.Center;

    /// <summary>Clicking the backdrop cancels it. Defaults to true.</summary>
    public bool CloseOnBackdrop { get; init; } = true;

    /// <summary>Shows the close button in the header. Defaults to true.</summary>
    public bool ShowCloseButton { get; init; } = true;

    /// <summary>
    /// The dialog's name for screen readers when it's opened without a title. A dialog with neither
    /// has no name.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// The dialog's color scheme. Dialogs from code are shown by the <see cref="DialogHost"/>, not
    /// where they were opened, so a part of the page with its own scheme passes it here to match.
    /// Defaults to <see cref="ColorScheme.Auto"/>: the page's.
    /// </summary>
    public ColorScheme ColorScheme { get; init; } = ColorScheme.Auto;

    /// <summary>The dialog's density, to match a part of the page with its own. Unset: the page's.</summary>
    public ThemeDensity? Density { get; init; }

    /// <summary>
    /// The dialog's brand color, to match a part of the page with its own: a built-in name or a hex
    /// color, as <see cref="IzzyTheme.Brand"/> takes. Unset: the page's.
    /// </summary>
    public string? Brand { get; init; }
}

/// <summary>How <see cref="DialogService.ConfirmAsync"/> asks.</summary>
public sealed class ConfirmOptions
{
    /// <summary>The confirm button's text. Defaults to <see cref="IzzyText.Ok"/> ("OK").</summary>
    public string? ConfirmText { get; init; }

    /// <summary>The cancel button's text. Defaults to <see cref="IzzyText.Cancel"/>.</summary>
    public string? CancelText { get; init; }

    /// <summary>The confirm button's color, such as <see cref="Accent.Danger"/> for a delete. Defaults to primary.</summary>
    public Accent Accent { get; init; } = Accent.Primary;
}

/// <summary>
/// The open dialog, cascaded to a component shown with <see cref="DialogService.OpenAsync{TComponent}"/>:
/// <c>[CascadingParameter] public DialogInstance Dialog { get; set; }</c>.
/// </summary>
public sealed class DialogInstance
{
    private readonly DialogService _service;

    internal DialogInstance(DialogService service, string id)
    {
        _service = service;
        Id = id;
    }

    /// <summary>The dialog element's id.</summary>
    public string Id { get; }

    /// <summary>Closes the dialog; the caller's <c>OpenAsync</c> returns this data.</summary>
    public void Close(object? data = null) => _service.Complete(Id, DialogResult.Ok(data));

    /// <summary>Closes the dialog as canceled.</summary>
    public void Cancel() => _service.Complete(Id, DialogResult.Cancel());
}

/// <summary>
/// Opens dialogs from code on interactive pages and awaits how they end, for flows like "edit this,
/// then refresh" or "are you sure?". Needs a <see cref="DialogHost"/> on the interactive page or
/// layout. Static pages can't open anything from code: use <see cref="Dialog"/> with a
/// <see cref="DialogTrigger"/> or a <c>?dialog=id</c> link, or <see cref="ConfirmButton"/>, there.
/// </summary>
/// <example>
/// <code>
/// var result = await Dialogs.OpenAsync&lt;EditSite&gt;("Edit site", new() { ["Site"] = site });
/// if (!result.Canceled) await Reload();
///
/// if (await Dialogs.ConfirmAsync("Remove this member?", "Remove", new() { Accent = Accent.Danger })) ...
/// </code>
/// </example>
public sealed class DialogService
{
    // Callers may be off the renderer's dispatcher (a timer, a hub callback), so the list is
    // changed under a lock and the host renders from a snapshot.
    private readonly List<DialogEntry> _open = [];
    private readonly Lock _gate = new();
    private readonly List<object> _hosts = [];
    private int _next;

    internal event Action? Changed;

    internal IReadOnlyList<DialogEntry> Open
    {
        get
        {
            lock (_gate)
            {
                return _open.ToArray();
            }
        }
    }

    /// <summary>Opens <typeparamref name="TComponent"/> in a dialog and waits until it closes.</summary>
    /// <param name="title">The dialog's heading, which also names it for screen readers.</param>
    /// <param name="parameters">The component's parameters, by name.</param>
    /// <param name="options">Size, placement and closing behavior.</param>
    public Task<DialogResult> OpenAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(string? title, Dictionary<string, object?>? parameters = null, DialogOptions? options = null)
        where TComponent : IComponent =>
        OpenAsync(typeof(TComponent), title, parameters, options);

    /// <summary>Opens a component of <paramref name="component"/>'s type in a dialog and waits until it closes.</summary>
    public Task<DialogResult> OpenAsync([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type component, string? title, Dictionary<string, object?>? parameters = null, DialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(component);
        options ??= new DialogOptions();

        // Checked here, so a bad value fails the caller rather than the host's render.
        if (!string.IsNullOrWhiteSpace(options.Brand))
        {
            ThemeColors.Parse(options.Brand, ThemeColors.Brands, nameof(DialogOptions.Brand));
        }

        var entry = new DialogEntry(new DialogInstance(this, $"ib-dialog-svc-{Interlocked.Increment(ref _next)}"), component, title, parameters, options);

        // One lock for the check and the add, so a host leaving in between can't strand the dialog.
        lock (_gate)
        {
            if (_hosts.Count == 0)
            {
                throw new InvalidOperationException(
                    "DialogService needs a <DialogHost /> on an interactive page or in an interactive layout. " +
                    "Static pages can't open dialogs from code; use <Dialog> with <DialogTrigger> or a ?dialog= link there.");
            }

            _open.Add(entry);
        }

        Changed?.Invoke();
        return entry.Result.Task;
    }

    /// <summary>Asks a yes-or-no question in a small dialog. True when confirmed.</summary>
    public async Task<bool> ConfirmAsync(string message, string? title = null, ConfirmOptions? options = null)
    {
        var result = await OpenAsync(
            typeof(ConfirmDialogContent),
            title,
            new Dictionary<string, object?> { [nameof(ConfirmDialogContent.Message)] = message, [nameof(ConfirmDialogContent.Options)] = options ?? new ConfirmOptions() },
            // Without a title, the question names the dialog.
            new DialogOptions { Size = DialogSize.Sm, CloseOnBackdrop = false, ShowCloseButton = false, Label = title is null ? message : null });
        return !result.Canceled && result.Data is true;
    }

    /// <summary>Closes the most recently opened dialog with this data.</summary>
    public void Close(object? data = null)
    {
        DialogEntry? top;
        lock (_gate)
        {
            top = _open.LastOrDefault(e => e.IsOpen);
        }

        if (top is not null)
        {
            Complete(top.Instance.Id, DialogResult.Ok(data));
        }
    }

    internal void Complete(string id, DialogResult result)
    {
        DialogEntry? entry;
        lock (_gate)
        {
            entry = _open.Find(e => e.Instance.Id == id);
        }

        if (entry is null || !entry.Result.TrySetResult(result))
        {
            return;
        }

        // Close it first, so the browser runs the closing transition and returns focus to where it
        // was; then take it out.
        entry.IsOpen = false;
        Changed?.Invoke();
        _ = RemoveLaterAsync(entry);
    }

    private async Task RemoveLaterAsync(DialogEntry entry)
    {
        await Task.Delay(400);
        lock (_gate)
        {
            _open.Remove(entry);
        }

        Changed?.Invoke();
    }

    // Only the most recent host shows the dialogs, so a second host (another layout, or the next
    // page's layout arriving before the last one is disposed) never shows them twice.
    internal void AddHost(object host)
    {
        lock (_gate)
        {
            _hosts.Add(host);
        }

        Changed?.Invoke();
    }

    internal bool IsActiveHost(object host)
    {
        lock (_gate)
        {
            return _hosts.Count > 0 && ReferenceEquals(_hosts[^1], host);
        }
    }

    internal void RemoveHost(object host)
    {
        DialogEntry[] closing = [];
        lock (_gate)
        {
            _hosts.Remove(host);
            if (_hosts.Count == 0)
            {
                closing = [.. _open];
                _open.Clear();
            }
        }

        foreach (var entry in closing)
        {
            entry.Result.TrySetResult(DialogResult.Cancel());
        }

        // The host before it, if any, takes over.
        Changed?.Invoke();
    }
}

/// <summary>One dialog opened by <see cref="DialogService"/>.</summary>
internal sealed class DialogEntry(DialogInstance instance, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type component, string? title, Dictionary<string, object?>? parameters, DialogOptions options)
{
    public DialogInstance Instance { get; } = instance;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public Type Component { get; } = component;

    public string? Title { get; } = title;

    public IDictionary<string, object> Parameters { get; } =
        parameters?.ToDictionary(p => p.Key, p => p.Value!) ?? new Dictionary<string, object>();

    public DialogOptions Options { get; } = options;

    public bool IsOpen { get; set; } = true;

    public TaskCompletionSource<DialogResult> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
