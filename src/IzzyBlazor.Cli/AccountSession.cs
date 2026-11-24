using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

/// <summary>
/// This machine's IzzyBlazor account: the signed key from <c>izzy login</c> and the account's copy of the
/// catalog for this version of izzy, both under <paramref name="home"/> (<c>~/.izzy</c>). The key is checked
/// here, with no network. The account service is called only to sign in, to renew a key over a day old, to
/// download the catalog after an update, to sign out, and to post what <c>izzy feedback</c> was told to;
/// nothing about what is generated is ever sent.
/// </summary>
/// <param name="home">The folder: <c>key</c> and <c>catalog/&lt;version&gt;/</c>.</param>
/// <param name="service">The account service's address.</param>
/// <param name="publicKeys">The service's public signing keys, by key id.</param>
/// <param name="version">This izzy's version, which the catalog goes with.</param>
/// <param name="ciKey">A key made on the account page for CI (<c>IZZY_KEY</c>); it wins over a stored sign-in.</param>
/// <param name="handler">The HTTP handler, for tests.</param>
/// <param name="clock">The time, for tests.</param>
/// <param name="open">Opens an address in the browser, for tests.</param>
/// <param name="wait">Waits between two questions to the service, for tests.</param>
sealed class AccountSession(string home, Uri service, IReadOnlyDictionary<string, string> publicKeys, string version, string? ciKey = null,
    HttpMessageHandler? handler = null, Func<DateTimeOffset>? clock = null, Action<string>? open = null, Action<TimeSpan>? wait = null)
{
    /// <summary>How long a renewal may hold up a command.</summary>
    static readonly TimeSpan RenewLimit = TimeSpan.FromSeconds(2);

    /// <summary>A key older than this is renewed when the machine is online.</summary>
    static readonly TimeSpan RenewAfter = TimeSpan.FromDays(1);

    readonly HttpClient _http = new(handler ?? new HttpClientHandler()) { BaseAddress = service, Timeout = TimeSpan.FromSeconds(60) };

    string KeyPath => Path.Combine(home, "key");
    string CatalogRoot => Path.Combine(home, "catalog");
    // Pro's copy has more in it than an account's, so each has its folder: when the plan changes, the copy on
    // the machine is the wrong one, and the right one is downloaded in its place.
    string CatalogFolder => Path.Combine(CatalogRoot, Plan == ItemTier.Pro ? $"{version}.pro" : version);
    DateTimeOffset Now => clock?.Invoke() ?? DateTimeOffset.UtcNow;

    /// <summary>The account service from the environment: <c>~/.izzy</c> (or <c>IZZY_HOME</c>) and <c>IZZY_KEY</c>.</summary>
    public static AccountSession FromEnvironment()
    {
        var home = Environment.GetEnvironmentVariable("IZZY_HOME") is { Length: > 0 } given
            ? given : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".izzy");
        var address = new Uri("https://account.izzyblazor.com/");
        var keys = AccountKeys.Public;
#if DEBUG
        // Against an account service on this machine: its address, and its public key as "<key id>=<base64>".
        if (Environment.GetEnvironmentVariable("IZZY_ACCOUNT_URL") is { Length: > 0 } url) address = new Uri(url);
        if (Environment.GetEnvironmentVariable("IZZY_ACCOUNT_KEYS") is { Length: > 0 } extra)
            keys = keys.Concat(extra.Split(';').Select(k => k.Split('=', 2)).Select(k => KeyValuePair.Create(k[0], k[1]))).ToDictionary();
#endif
        return new AccountSession(home, address, keys, Generator.IzzyBlazorVersion,
            Environment.GetEnvironmentVariable("IZZY_KEY") is { Length: > 0 } ci ? ci : null);
    }

    /// <summary>Whether this machine has a sign-in or a CI key.</summary>
    public bool SignedIn => CiKey || ReadKey(out _) is not null;

    /// <summary>
    /// What this machine's sign-in reaches: nothing signed out, an account's items, or Pro's. A CI key is an
    /// account's. A key past its date keeps the account's items, which are free, and not Pro: the key's 30
    /// days are how long Pro goes on without the service.
    /// </summary>
    public ItemTier Plan => CiKey ? ItemTier.Account : ReadKey(out var state) is not { } key ? ItemTier.Free
        : key.Tier == ItemTier.Pro && state == AccountKeyState.Expired ? ItemTier.Account : key.Tier;

    /// <summary>Whether Pro is here as a preview: the account reserved the founding price, and Pro isn't on sale yet.</summary>
    public bool Preview => !CiKey && Plan == ItemTier.Pro && ReadKey(out _) is { Preview: true };

    /// <summary>
    /// Asks the service once for this machine's key as the account stands now, when the key doesn't say Pro:
    /// a reservation made since the key was signed brings Pro's copy of the catalog with it. Called before a
    /// Pro command says it needs Pro. True when the key says Pro afterwards.
    /// </summary>
    public bool Upgrade(TextWriter error)
    {
        if (CiKey || ReadKey(out _) is null) return false;
        if (Plan == ItemTier.Pro) return true;
        if (Renew(File.ReadAllText(KeyPath).Trim()) is not { Length: > 0 } renewed || Plan != ItemTier.Pro) return false;
        if (!Directory.Exists(CatalogFolder) && Download(renewed) is { } problem) error.WriteLine(problem);
        return true;
    }

    /// <summary>What a Pro command says once when Pro is a preview.</summary>
    public const string PreviewNote = "Pro preview: free until Pro goes on sale. Tell us what works and what doesn't: izzy feedback.";

    // IZZY_KEY holds a key made on the account page: only the service can say whether it's a live one, but a
    // value that isn't one at all is known here, and unlocks nothing.
    bool CiKey => ciKey is { Length: >= 40 } && ciKey.StartsWith("izzyci_", StringComparison.Ordinal);

    const string NotCiKey = "IZZY_KEY isn't a key from your account page (those start with izzyci_). Make one there, under Keys for CI.";

    /// <summary>
    /// The account's copy of the catalog on this machine, or null when there is none. A command that generates
    /// (<paramref name="online"/>) first renews a key over a day old and downloads the catalog if this version's
    /// is missing; one that only lists never calls the service.
    /// </summary>
    public string? Catalog(TextWriter error, bool online)
    {
        string credential;
        if (ciKey is not null && !CiKey)
        {
            error.WriteLine(NotCiKey);
            return null;
        }
        if (ciKey is not null) credential = ciKey;
        else
        {
            var key = ReadKey(out var state);
            if (key is null) return null;
            credential = File.ReadAllText(KeyPath).Trim();
            if (online && (state == AccountKeyState.Expired || Now - key.Issued > RenewAfter))
            {
                switch (Renew(credential))
                {
                    case { } renewed when renewed.Length > 0:
                        credential = renewed;
                        state = AccountKeyState.Valid;
                        break;
                    case "":
                        Clear();
                        error.WriteLine("This machine was signed out of its IzzyBlazor account. Run izzy login.");
                        return null;
                }
            }
            // Offline with a key past its date: the templates are free and the code is the developer's, so it
            // goes on working and renews when it can.
            if (state == AccountKeyState.Expired)
                error.WriteLine("Your IzzyBlazor sign-in needs renewing. izzy renews it the next time this machine is online.");
        }
        if (!Directory.Exists(CatalogFolder) && online && Download(credential) is { } problem)
            error.WriteLine(problem);
        return Directory.Exists(CatalogFolder) ? CatalogFolder : null;
    }

    // The account page lists each machine with its system, so two with like names can be told apart.
    static string Os => OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsWindows() ? "Windows" : "Linux";

    /// <summary>izzy login: the device sign-in. A code in the terminal, approved in the browser.</summary>
    public int Login(TextWriter output, TextWriter error, bool browser)
    {
        JsonNode start;
        try
        {
            using var response = _http.PostAsJsonAsync("v1/device/start", new JsonObject { ["machine"] = Environment.MachineName, ["version"] = version, ["os"] = Os }).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            start = JsonNode.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult())!;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            error.WriteLine($"Can't sign in at {service.Host} right now. Check the connection and run izzy login again.");
            return 1;
        }
        var address = (string?)start["verification_uri_complete"] ?? (string)start["verification_uri"]!;
        output.WriteLine($"Your code: {(string)start["user_code"]!}");
        output.WriteLine($"Open {address}");
        output.WriteLine("Check that the page shows the same code, then approve it. Waiting…");
        if (browser) (open ?? OpenBrowser)(address);

        var interval = TimeSpan.FromSeconds((int?)start["interval"] ?? 5);
        var until = Now + TimeSpan.FromSeconds((int?)start["expires_in"] ?? 900);
        while (Now < until)
        {
            (wait ?? Thread.Sleep)(interval);
            try
            {
                using var response = _http.PostAsJsonAsync("v1/device/token", new JsonObject { ["device_code"] = (string)start["device_code"]! }).GetAwaiter().GetResult();
                var body = JsonNode.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                if (response.IsSuccessStatusCode)
                {
                    var text = (string)body!["key"]!;
                    if (AccountKey.Read(text, publicKeys, Now, out var state) is null || state != AccountKeyState.Valid)
                    {
                        error.WriteLine("The sign-in came back with a key this version of izzy can't check. Update izzy and run izzy login again.");
                        return 1;
                    }
                    Save(text);
                    if (Download(text) is { } problem)
                    {
                        output.WriteLine("Signed in.");
                        error.WriteLine(problem);
                        return 0;
                    }
                    output.WriteLine("Signed in. The starters, blocks and kits that come with your account are on this machine: izzy list shows them.");
                    return 0;
                }
                switch ((string?)body?["error"])
                {
                    case "authorization_pending": break;
                    case "slow_down": interval += TimeSpan.FromSeconds(5); break;
                    case "access_denied":
                        error.WriteLine("The sign-in was canceled.");
                        return 1;
                    default:
                        error.WriteLine("The code expired. Run izzy login again.");
                        return 1;
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                // A dropped request: ask again until the code runs out.
            }
        }
        error.WriteLine("The code expired. Run izzy login again.");
        return 1;
    }

    /// <summary>izzy logout: the key and the catalog leave this machine, and the service forgets the machine.</summary>
    public int Logout(TextWriter output)
    {
        if (ReadKey(out _) is null && !Directory.Exists(CatalogRoot))
        {
            output.WriteLine(ciKey is null ? "Not signed in." : "Not signed in. IZZY_KEY is set in the environment: unset it there.");
            return 0;
        }
        if (File.Exists(KeyPath))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "v1/logout") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", File.ReadAllText(KeyPath).Trim()) } };
                using var cancel = new CancellationTokenSource(RenewLimit);
                _http.SendAsync(request, cancel.Token).GetAwaiter().GetResult().Dispose();
            }
            catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
            {
                // Offline: the machine stays listed on the account page until its key runs out.
            }
        }
        Clear();
        output.WriteLine("Signed out. The account's templates were removed from this machine.");
        return 0;
    }

    /// <summary>izzy account: who this machine is signed in as.</summary>
    public int Status(TextWriter output, bool json = false)
    {
        if (json)
        {
            // For an editor: "free" is an account without Pro; signed out, there is no plan.
            var signedIn = ciKey is not null ? CiKey : ReadKey(out _) is not null;
            output.WriteLine(new JsonObject { ["signedIn"] = signedIn, ["plan"] = !signedIn ? null : Preview ? "preview" : Plan == ItemTier.Pro ? "pro" : "free" }.ToJsonString());
            return 0;
        }
        if (ciKey is not null)
        {
            output.WriteLine(CiKey ? "Using the key in IZZY_KEY." : NotCiKey);
            return 0;
        }
        var key = ReadKey(out var state);
        if (key is null)
        {
            output.WriteLine("Not signed in. Run izzy login.");
            return 0;
        }
        output.WriteLine($"Signed in. Plan: {(key.Tier != ItemTier.Pro ? "Free" : key.Preview ? "Pro preview" : "Pro")}.");
        if (key is { Tier: ItemTier.Pro, Preview: true })
            output.WriteLine("You reserved the founding price, so Pro is yours to use until it goes on sale. Tell us what works and what doesn't: izzy feedback.");
        output.WriteLine(state == AccountKeyState.Expired
            ? "The sign-in needs renewing. izzy renews it the next time this machine is online."
            : $"The sign-in renews by itself, by {key.Expires.ToLocalTime():MMMM d, yyyy} at the latest.");
        output.WriteLine($"Manage the account at {new Uri(service, "/").AbsoluteUri.TrimEnd('/')}.");
        return 0;
    }

    /// <summary>This izzy and the machine's system, as a post says them: "0.1.0 on macOS".</summary>
    public string Where => $"{version} on {Os}";

    /// <summary>
    /// Why this machine can't send feedback, with the exit code, before anyone is asked to write it; null
    /// when it can. A sign-in that doesn't say Pro is renewed first: the reservation may be newer than the key.
    /// </summary>
    public (int Exit, string Message)? NoFeedback()
    {
        if (ReadKey(out _) is null)
            return (4, ciKey is null ? "izzy feedback needs your IzzyBlazor account on this machine. Run izzy login."
                : "izzy feedback needs a sign-in on this machine, not the key in IZZY_KEY. Run izzy login.");
        return Upgrade(TextWriter.Null) ? null : (3, NotProFeedback);
    }

    const string NotProFeedback = "izzy feedback comes with IzzyBlazor Pro and its preview. Anyone can write at https://github.com/enabledevmode/izzyblazor/discussions.";

    /// <summary>
    /// izzy feedback: posts what the person wrote. Exit code 0 with the post's address, or the code and what
    /// to tell them.
    /// </summary>
    public (int Exit, string Message) SendFeedback(string text, string? about, string? from)
    {
        if (!File.Exists(KeyPath)) return (4, "izzy feedback needs your IzzyBlazor account on this machine. Run izzy login.");
        const string later = "Nothing was posted. Try again later, or write to support@izzyblazor.com.";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/feedback")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", File.ReadAllText(KeyPath).Trim()) },
                Content = JsonContent.Create(new JsonObject { ["text"] = text, ["about"] = about, ["version"] = version, ["os"] = Os, ["from"] = from }),
            };
            using var response = _http.SendAsync(request).GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var answer = body.Length > 0 && response.Content.Headers.ContentType?.MediaType == "application/json" ? JsonNode.Parse(body) : null;
            if (response.IsSuccessStatusCode && (string?)answer?["url"] is { } url) return (0, url);
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => (4, "The account service didn't accept this machine's sign-in. Run izzy login."),
                HttpStatusCode.Forbidden => (3, NotProFeedback),
                // An izzy from before the service took feedback, or a service from before it.
                HttpStatusCode.NotFound => (1, $"{service.Host} doesn't take feedback from izzy yet. Write to support@izzyblazor.com."),
                _ => (1, (string?)answer?["message"] ?? $"{service.Host} couldn't take the feedback. {later}"),
            };
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return (1, $"{service.Host} can't be reached. {later}");
        }
    }

    AccountKey? ReadKey(out AccountKeyState state)
    {
        state = AccountKeyState.Invalid;
        return File.Exists(KeyPath) ? AccountKey.Read(File.ReadAllText(KeyPath), publicKeys, Now, out state) : null;
    }

    void Save(string key)
    {
        Directory.CreateDirectory(home);
        // The owner's alone from the moment it exists, not made readable and then closed.
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using var file = new StreamWriter(KeyPath, new System.Text.UTF8Encoding(false), options);
        file.Write(key + "\n");
    }

    void Clear()
    {
        if (File.Exists(KeyPath)) File.Delete(KeyPath);
        if (Directory.Exists(CatalogRoot)) Directory.Delete(CatalogRoot, recursive: true);
    }

    // The new key; "" when the service refuses this one (signed out, or the account is gone); null when it
    // can't be reached in time, which changes nothing.
    string? Renew(string key)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/key/renew") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", key) } };
            using var cancel = new CancellationTokenSource(RenewLimit);
            using var response = _http.SendAsync(request, cancel.Token).GetAwaiter().GetResult();
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return "";
            if (!response.IsSuccessStatusCode) return null;
            var renewed = (string?)JsonNode.Parse(response.Content.ReadAsStringAsync(cancel.Token).GetAwaiter().GetResult())?["key"];
            if (AccountKey.Read(renewed, publicKeys, Now, out var state) is null || state != AccountKeyState.Valid) return null;
            Save(renewed!);
            return renewed;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    // Downloads this version's account catalog, checks its signature and unpacks it; null when done, else
    // what to tell the developer.
    string? Download(string credential)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/catalog/{Uri.EscapeDataString(version)}") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", credential) } };
            using var response = _http.SendAsync(request).GetAwaiter().GetResult();
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return ciKey is null ? "The account service didn't accept this machine's sign-in. Run izzy login." : "The account service didn't accept the key in IZZY_KEY. Make a new one on your account page.";
            if (response.StatusCode == HttpStatusCode.NotFound)
                return $"The account's templates for izzy {version} aren't published yet. Update izzy, or try again later.";
            response.EnsureSuccessStatusCode();
            var bundle = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            var signature = response.Headers.TryGetValues("X-Izzy-Signature", out var values) ? values.FirstOrDefault() : null;
            if (!AccountBundle.Verify(bundle, signature, publicKeys))
                return "The downloaded templates aren't signed by IzzyBlazor, so they weren't kept. Update izzy and try again.";
            // One version's catalog at a time: an update replaces the last.
            if (Directory.Exists(CatalogRoot)) Directory.Delete(CatalogRoot, recursive: true);
            Directory.CreateDirectory(CatalogRoot);
            AccountBundle.Unpack(bundle, CatalogFolder);
            return null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return $"The account's templates for izzy {version} aren't on this machine yet, and {service.Host} can't be reached. Connect and run the command again.";
        }
    }

    static void OpenBrowser(string address)
    {
        try
        {
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            // No browser here (SSH, a container): the address is on screen.
        }
    }
}
