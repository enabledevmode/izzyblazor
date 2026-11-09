using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Reading a chart's data again on an interval (Refresh), for the charts that refresh alone (no
/// Pause button): on a static page the script fetches the page again (chart-live.ts), and on an
/// interactive page a timer raises OnRefresh. Chart has its own, with pausing.
/// </summary>
internal sealed class ChartRefresh : IDisposable
{
    private CancellationTokenSource? _ticking;
    private TimeSpan? _every;

    /// <summary>On a static page, the interval in milliseconds for the script; otherwise null.</summary>
    public string? Every { get; private set; }

    /// <summary>On a static page, RefreshFrom for the script; otherwise null.</summary>
    public string? From { get; private set; }

    /// <summary>Checks Refresh as the component's parameters arrive: at least a second, an id on a static page, OnRefresh on an interactive one.</summary>
    public void Check(string component, TimeSpan? refresh, string? from, RendererInfo renderer, IComponentRenderMode? assigned, IReadOnlyDictionary<string, object>? attributes, bool hasOnRefresh)
    {
        Every = From = null;
        if (refresh is not { } every) return;
        if (every < TimeSpan.FromSeconds(1)) throw new InvalidOperationException($"{component} Refresh is at least a second, such as TimeSpan.FromSeconds(5).");
        if (!renderer.IsInteractive && assigned is null)
        {
            if (!(attributes?.TryGetValue("id", out var id) == true && !string.IsNullOrWhiteSpace(id?.ToString())))
            {
                throw new InvalidOperationException($"{component} Refresh on a static page needs an id, such as id=\"sales\": the page is fetched again and the chart found in it by its id.");
            }

            Every = ((long)every.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
            From = from;
        }
        else if (renderer.IsInteractive && !hasOnRefresh)
        {
            throw new InvalidOperationException($"{component} Refresh on an interactive page raises OnRefresh: handle it to load new data.");
        }
    }

    /// <summary>After each render on an interactive page: the timer, started again when Refresh changes.</summary>
    public void AfterRender(TimeSpan? refresh, RendererInfo renderer, EventCallback onRefresh, Func<Func<Task>, Task> invoke, Func<Exception, Task> dispatch)
    {
        if (!renderer.IsInteractive || refresh == _every) return;
        Stop();
        _every = refresh;
        if (refresh is not { } every || !onRefresh.HasDelegate) return;
        _ticking = new CancellationTokenSource();
        _ = Tick(every, _ticking.Token, onRefresh, invoke, dispatch);
    }

    private static async Task Tick(TimeSpan every, CancellationToken token, EventCallback onRefresh, Func<Func<Task>, Task> invoke, Func<Exception, Task> dispatch)
    {
        using var timer = new PeriodicTimer(every);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                await invoke(async () =>
                {
                    try { await onRefresh.InvokeAsync(); }
                    catch (Exception ex) { await dispatch(ex); }
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Stop()
    {
        _ticking?.Cancel();
        _ticking?.Dispose();
        _ticking = null;
    }

    /// <inheritdoc />
    public void Dispose() => Stop();
}
