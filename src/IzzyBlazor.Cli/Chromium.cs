using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

/// <summary>
/// A headless Chrome, Edge or Chromium that is already on the machine, driven over its debugging protocol:
/// izzy starts it with a profile of its own, talks to its one page over a WebSocket and ends it. Nothing is
/// downloaded, and the person's own browser profile is never opened.
/// </summary>
sealed class Chromium : IAsyncDisposable
{
    readonly Process _process;
    readonly string _profile;
    readonly ClientWebSocket _socket = new();
    readonly ConcurrentDictionary<int, TaskCompletionSource<JsonNode>> _pending = new();
    readonly Channel<JsonNode> _events = Channel.CreateUnbounded<JsonNode>();
    readonly CancellationTokenSource _closed = new();

    /// <summary>Whether the browser is gone: closed here, or dead on its own.</summary>
    public bool Closed => _closed.IsCancellationRequested || _socket.State != System.Net.WebSockets.WebSocketState.Open;
    IDisposable? _interrupt;
    int _id;

    Chromium(Process process, string profile) => (_process, _profile) = (process, profile);

    /// <summary>
    /// The browser to use: the one given (<c>--browser</c>, then <c>IZZY_BROWSER</c>), else Chrome, Edge or
    /// Chromium where each system installs it; null when there is none.
    /// </summary>
    public static string? Find(string? given)
    {
        if ((given ?? Environment.GetEnvironmentVariable("IZZY_BROWSER")) is { Length: > 0 } path)
            return File.Exists(path) ? path : throw new UsageException($"No browser at {path}.");
        IEnumerable<string> candidates;
        if (OperatingSystem.IsWindows())
        {
            string[] roots = [Environment.GetEnvironmentVariable("ProgramFiles") ?? "", Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "", Environment.GetEnvironmentVariable("LocalAppData") ?? ""];
            string[] apps = [@"Google\Chrome\Application\chrome.exe", @"Microsoft\Edge\Application\msedge.exe", @"Chromium\Application\chrome.exe"];
            candidates = apps.SelectMany(app => roots.Where(r => r.Length > 0).Select(r => Path.Combine(r, app)));
        }
        else if (OperatingSystem.IsMacOS())
        {
            string[] roots = ["/Applications", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications")];
            string[] apps = ["Google Chrome.app/Contents/MacOS/Google Chrome", "Microsoft Edge.app/Contents/MacOS/Microsoft Edge", "Chromium.app/Contents/MacOS/Chromium"];
            candidates = apps.SelectMany(app => roots.Select(r => Path.Combine(r, app)));
        }
        else
        {
            string[] names = ["google-chrome", "google-chrome-stable", "chromium", "chromium-browser", "microsoft-edge", "microsoft-edge-stable"];
            var folders = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            candidates = names.SelectMany(name => folders.Select(f => Path.Combine(f, name)));
        }
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>Starts the browser at <paramref name="path"/> and connects to its page.</summary>
    public static async Task<Chromium> StartAsync(string path)
    {
        var profile = Directory.CreateTempSubdirectory("izzy-browser-").FullName;
        var start = new ProcessStartInfo(path) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        string[] arguments = ["--headless=new", "--remote-debugging-port=0", $"--user-data-dir={profile}", "--no-first-run", "--no-default-browser-check", "--disable-extensions", "--disable-gpu"];
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        // Chromium's sandbox refuses to start as root (a container in CI).
        if (OperatingSystem.IsLinux() && Environment.UserName == "root") start.ArgumentList.Add("--no-sandbox");
        start.ArgumentList.Add("about:blank");
        Process process;
        try { process = Process.Start(start) ?? throw new GenerationException($"{path} didn't start."); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or GenerationException)
        {
            try { Directory.Delete(profile, recursive: true); } catch (Exception x) when (x is IOException or UnauthorizedAccessException) { }
            throw new GenerationException(e is GenerationException ? e.Message : $"{path} couldn't be started as a browser: {e.Message}");
        }
        var browser = new Chromium(process, profile);
        AppDomain.CurrentDomain.ProcessExit += browser.Kill;
        browser._interrupt = Interrupt.On(browser.End);
        try
        {
            // The browser writes where it listens in its profile: DevToolsActivePort, the port on its first line.
            // A busy machine (a build server) can take most of a minute to get there.
            var said = new StringBuilder();
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, e) => { lock (said) said.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var port = 0;
            var file = Path.Combine(profile, "DevToolsActivePort");
            var patience = System.Diagnostics.Stopwatch.StartNew();
            while (port == 0 && patience.Elapsed < TimeSpan.FromSeconds(90) && !process.HasExited)
            {
                try { if (File.Exists(file) && int.TryParse(File.ReadLines(file).FirstOrDefault(), out var written) && written > 0) port = written; }
                catch (IOException) { }
                if (port == 0) await Task.Delay(100);
            }
            if (port == 0)
            {
                string errors;
                lock (said) errors = said.ToString().Trim();
                throw new GenerationException($"{path} {(process.HasExited ? "closed as soon as it started" : "started and didn't open for izzy in 90 seconds")}{(errors.Length > 0 ? $":\n{errors}" : ".")}");
            }

            using var http = new HttpClient();
            var targets = JsonNode.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/json/list"))!.AsArray();
            var page = targets.FirstOrDefault(t => (string?)t!["type"] == "page") ?? throw new GenerationException($"{path} has no page to drive.");
            await browser._socket.ConnectAsync(new Uri((string)page["webSocketDebuggerUrl"]!), CancellationToken.None);
            _ = browser.ReceiveAsync();
            return browser;
        }
        catch (Exception e) when (e is OperationCanceledException or HttpRequestException or WebSocketException)
        {
            await browser.DisposeAsync();
            throw new GenerationException($"{path} started and izzy couldn't connect to it: {e.Message}");
        }
        catch
        {
            await browser.DisposeAsync();
            throw;
        }
    }

    /// <summary>Sends a command and answers with its result; a command the browser refuses throws.</summary>
    public async Task<JsonNode> Send(string method, JsonObject? parameters = null)
    {
        var id = Interlocked.Increment(ref _id);
        var answer = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = answer;
        var message = new JsonObject { ["id"] = id, ["method"] = method, ["params"] = parameters ?? [] };
        JsonNode reply;
        try
        {
            await _socket.SendAsync(Encoding.UTF8.GetBytes(message.ToJsonString()), WebSocketMessageType.Text, true, _closed.Token);
            reply = await answer.Task.WaitAsync(TimeSpan.FromSeconds(120));
        }
        catch (TimeoutException)
        {
            _pending.TryRemove(id, out _);
            throw new GenerationException("The browser didn't answer in two minutes.");
        }
        catch (Exception e) when (e is WebSocketException or InvalidOperationException or OperationCanceledException)
        {
            // The browser is gone (it crashed, or was closed): every page after this one says so too.
            _pending.TryRemove(id, out _);
            throw new GenerationException("The browser closed before it answered.");
        }
        if (reply["error"] is { } error) throw new GenerationException($"The browser refused {method}: {(string?)error["message"]}");
        return reply["result"] ?? new JsonObject();
    }

    /// <summary>Runs JavaScript in the page and answers with its value (a promise is waited for).</summary>
    public async Task<JsonNode?> Evaluate(string script)
    {
        var result = await Send("Runtime.evaluate", new JsonObject { ["expression"] = script, ["awaitPromise"] = true, ["returnByValue"] = true });
        if (result["exceptionDetails"] is { } thrown)
            throw new GenerationException((string?)thrown["exception"]?["description"] ?? (string?)thrown["text"] ?? "The page's script failed.");
        return result["result"]?["value"];
    }

    /// <summary>
    /// The next event that <paramref name="wanted"/> accepts, or null when none comes in time. Events that
    /// came before it are dropped.
    /// </summary>
    public async Task<JsonNode?> Event(Func<JsonNode, bool> wanted, TimeSpan patience)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(_closed.Token);
        limit.CancelAfter(patience);
        try
        {
            while (true)
            {
                var next = await _events.Reader.ReadAsync(limit.Token);
                if (wanted(next)) return next;
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ChannelClosedException) { return null; }
    }

    /// <summary>Forgets the events that have come so far.</summary>
    public void Drain()
    {
        while (_events.Reader.TryRead(out _)) { }
    }

    async Task ReceiveAsync()
    {
        var buffer = new byte[1 << 16];
        var message = new MemoryStream();
        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                var read = await _socket.ReceiveAsync(buffer, _closed.Token);
                if (read.MessageType == WebSocketMessageType.Close) break;
                message.Write(buffer, 0, read.Count);
                if (!read.EndOfMessage) continue;
                var node = JsonNode.Parse(message.GetBuffer().AsSpan(0, (int)message.Length))!;
                message.SetLength(0);
                if ((int?)node["id"] is { } id)
                {
                    if (_pending.TryRemove(id, out var answer)) answer.TrySetResult(node);
                }
                else _events.Writer.TryWrite(node);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException or ObjectDisposedException) { }
        finally
        {
            _events.Writer.TryComplete();
            foreach (var waiting in _pending.Values) waiting.TrySetException(new GenerationException("The browser closed before it answered."));
        }
    }

    void Kill(object? sender, EventArgs e)
    {
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
        catch (Exception x) when (x is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    // izzy is being stopped (Ctrl+C, a build server's cancel): the browser and its profile go with it.
    void End()
    {
        Kill(null, EventArgs.Empty);
        try
        {
            _process.WaitForExit(2000);
            Directory.Delete(_profile, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    public async ValueTask DisposeAsync()
    {
        _interrupt?.Dispose();
        AppDomain.CurrentDomain.ProcessExit -= Kill;
        _closed.Cancel();
        Kill(null, EventArgs.Empty);
        try { await _process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token); } catch (OperationCanceledException) { }
        _socket.Dispose();
        _process.Dispose();
        try { Directory.Delete(_profile, recursive: true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>
/// What to end when izzy itself is stopped by a signal (Ctrl+C, a terminal closed, a build server's cancel):
/// a <c>using</c> block doesn't run then, so a browser or an app izzy started would outlive it.
/// </summary>
static class Interrupt
{
    static readonly List<Action> Endings = [];
    static List<System.Runtime.InteropServices.PosixSignalRegistration>? _signals;

    /// <summary>Runs <paramref name="end"/> if izzy is stopped before the returned value is disposed.</summary>
    public static IDisposable On(Action end)
    {
        lock (Endings)
        {
            Endings.Add(end);
            // After the handler the signal does what it always does: izzy ends.
            _signals ??= [.. ((System.Runtime.InteropServices.PosixSignal[])[System.Runtime.InteropServices.PosixSignal.SIGINT, System.Runtime.InteropServices.PosixSignal.SIGTERM, System.Runtime.InteropServices.PosixSignal.SIGHUP, System.Runtime.InteropServices.PosixSignal.SIGQUIT])
                .Select(signal => System.Runtime.InteropServices.PosixSignalRegistration.Create(signal, _ => EndAll()))];
        }
        return new Registration(end);
    }

    static void EndAll()
    {
        List<Action> endings;
        lock (Endings)
        {
            endings = [.. Endings];
            Endings.Clear();
        }
        foreach (var end in endings) end();
    }

    sealed class Registration(Action end) : IDisposable
    {
        public void Dispose()
        {
            lock (Endings) Endings.Remove(end);
        }
    }
}
