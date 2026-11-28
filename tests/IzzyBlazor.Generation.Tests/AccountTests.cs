using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using IzzyBlazor.Cli;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// An IzzyBlazor account on a machine: the signed key, the account's copy of the catalog, and izzy login from
/// the code in the terminal to a starter created from that copy, against a stand-in for the account service.
/// </summary>
public sealed class AccountTests : IDisposable
{
    static readonly DateTimeOffset Now = new(2026, 12, 1, 12, 0, 0, TimeSpan.Zero);

    readonly string _home = Directory.CreateTempSubdirectory("izzy-home-").FullName;
    readonly ECDsa _signing = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    Dictionary<string, string> PublicKeys => new() { ["k1"] = Convert.ToBase64String(_signing.ExportSubjectPublicKeyInfo()) };

    [Fact]
    public void A_key_is_valid_only_as_signed_and_expired_after_its_last_day()
    {
        var key = new AccountKey("acct_1", ItemTier.Account, "m_1", "k1", Now.AddDays(-1), Now.AddDays(29)).Sign(_signing);

        Assert.Equal("acct_1", AccountKey.Read(key, PublicKeys, Now, out var state)!.Account);
        Assert.Equal(AccountKeyState.Valid, state);

        AccountKey.Read(key, PublicKeys, Now.AddDays(40), out state);
        Assert.Equal(AccountKeyState.Expired, state);

        // The payload says Pro, the signature is still the free key's.
        var parts = key.Split('.');
        var pro = AccountKey.Base64Url(System.Text.Encoding.UTF8.GetBytes(
            System.Text.Encoding.UTF8.GetString(AccountKey.FromBase64Url(parts[1])).Replace("\"account\"}", "\"pro\"}").Replace("\"tier\":\"account\"", "\"tier\":\"pro\"")));
        Assert.Null(AccountKey.Read($"{parts[0]}.{pro}.{parts[2]}", PublicKeys, Now, out state));
        Assert.Equal(AccountKeyState.Invalid, state);

        // Signed by someone else under the same key id.
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Null(AccountKey.Read(new AccountKey("acct_1", ItemTier.Pro, "m_1", "k1", Now, Now.AddDays(30)).Sign(other), PublicKeys, Now, out _));
    }

    [AccountFact]
    public void An_accounts_copy_holds_the_account_items_and_nothing_of_Pro()
    {
        var bundle = AccountBundle.Export(Path.Combine(ProjectBuild.RepoRoot, "catalog"), ProjectBuild.PrivateCatalog!);
        using var zip = new ZipArchive(new MemoryStream(bundle));
        var ids = zip.Entries.Select(e => e.FullName.Split('/')[0]).Distinct().ToList();

        var all = Catalog.Load(Path.Combine(ProjectBuild.RepoRoot, "catalog"), ProjectBuild.PrivateCatalog);
        // With the items, the pack izzy audit judges one page's findings with.
        Assert.Equal(all.Items.Values.Where(i => i.Manifest.Tier == ItemTier.Account).Select(i => i.Manifest.Id).Append(".audit").Order(StringComparer.Ordinal), ids.Order(StringComparer.Ordinal));
        Assert.Contains(zip.Entries, e => e.FullName == ".audit/pack.json");
        // crud's Pages mode is Pro: its files are in the private catalog and stay there.
        Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("crud/", StringComparison.Ordinal));
    }

    [AccountFact]
    public void Izzy_login_brings_the_key_and_the_catalog_and_logout_removes_them()
    {
        var service = new Service(_signing, AccountBundle.Export(Path.Combine(ProjectBuild.RepoRoot, "catalog"), ProjectBuild.PrivateCatalog!));
        string? opened = null;
        var version = "9.9.9";
        (int Exit, string Out, string Error) Izzy(params string[] args)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var session = new AccountSession(_home, new Uri("https://account.test/"), PublicKeys, version, handler: service,
                clock: () => Now, open: address => opened = address, wait: _ => { });
            var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _home, output, error, account: session).Run(args);
            return (exit, output.ToString(), error.ToString());
        }

        var before = Izzy("new", "starter", "sidebar", "-n", "Acme", "--dry-run");
        Assert.Equal(4, before.Exit);
        Assert.EndsWith("Run izzy login.", before.Error.TrimEnd());

        var login = Izzy("login");
        Assert.Equal(0, login.Exit);
        Assert.Contains("Your code: WDJB-MJHT", login.Out);
        Assert.Equal("https://account.test/device?code=WDJB-MJHT", opened);
        Assert.Equal(2, service.TokenRequests);

        // From here the service isn't needed: the key is a day old at most, the catalog is on the machine.
        service.Down = true;
        var created = Izzy("new", "starter", "sidebar", "-n", "Acme", "--dry-run");
        Assert.Equal(0, created.Exit);
        Assert.Contains("Components/Layout/MainLayout.razor", created.Out);
        Assert.Contains("Plan: Free", Izzy("account").Out);

        // A newer izzy whose templates aren't published yet says so, and not that the item needs an account.
        (service.Down, version) = (false, "9.9.10");
        var unpublished = Izzy("new", "starter", "sidebar", "-n", "Acme", "--dry-run");
        Assert.Equal(4, unpublished.Exit);
        Assert.Equal("The account's templates for izzy 9.9.10 aren't published yet. Update izzy, or try again later.", unpublished.Error.Trim());
        version = "9.9.9";

        Assert.Equal(0, Izzy("logout").Exit);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_home));
        Assert.Equal(4, Izzy("new", "starter", "sidebar", "-n", "Acme", "--dry-run").Exit);
    }

    [AccountFact]
    public void A_reserved_founding_price_brings_Pro_as_a_preview_with_the_audit_pack()
    {
        var bundle = AccountBundle.Export(Path.Combine(ProjectBuild.RepoRoot, "catalog"), ProjectBuild.PrivateCatalog!, ItemTier.Pro);
        var service = new Service(_signing, bundle) { Preview = true };
        (int Exit, string Out, string Error) Izzy(params string[] args)
        {
            var (output, error) = (new StringWriter(), new StringWriter());
            var session = new AccountSession(_home, new Uri("https://account.test/"), PublicKeys, "9.9.9", handler: service, clock: () => Now, open: _ => { }, wait: _ => { });
            var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _home, output, error, account: session).Run(args);
            return (exit, output.ToString(), error.ToString());
        }

        Assert.Equal(0, Izzy("login").Exit);
        Assert.Contains("Plan: Pro preview", Izzy("account").Out);
        Assert.Equal("""{"signedIn":true,"plan":"preview"}""", Izzy("account", "--json").Out.Trim());
        // Pro's copy of the catalog: the pack izzy audit judges with, and crud's Pro pages.
        Assert.True(File.Exists(Path.Combine(_home, "catalog", "9.9.9.pro", ".audit", "pack.json")));
        var pages = Izzy("describe", "crud", "--json");
        Assert.True(pages.Exit == 0, pages.Error);
    }

    [Fact]
    public void Izzy_feedback_shows_the_post_and_sends_it_on_a_yes()
    {
        using var bundle = new MemoryStream();
        using (var zip = new ZipArchive(bundle, ZipArchiveMode.Create, leaveOpen: true)) zip.CreateEntry("starter/item.json");
        var service = new Service(_signing, bundle.ToArray()) { Preview = true };
        (int Exit, string Out, string Error) Izzy(string typed, params string[] args)
        {
            var (output, error) = (new StringWriter(), new StringWriter());
            var session = new AccountSession(_home, new Uri("https://account.test/"), PublicKeys, "9.9.9", handler: service, clock: () => Now, open: _ => { }, wait: _ => { });
            var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _home, output, error, account: session, input: new StringReader(typed)).Run(args);
            return (exit, output.ToString(), error.ToString());
        }

        Assert.Equal(4, Izzy("", "feedback").Exit);
        Assert.Equal(0, Izzy("", "login").Exit);

        // Asked what happened and which command, shown the post, and nothing goes on a no.
        var declined = Izzy("The task list repeats a finding\nfor each width.\n\nizzy audit --format tasks\nn\n", "feedback");
        Assert.Equal(0, declined.Exit);
        Assert.Contains("  The task list repeats a finding\n  for each width.\n\n  Command: izzy audit --format tasks\n  izzy 9.9.9 on ", declined.Out);
        Assert.Empty(service.Feedback);

        var posted = Izzy("The task list repeats a finding\nfor each width.\n\nizzy audit --format tasks\ny\n", "feedback");
        Assert.Equal(0, posted.Exit);
        Assert.EndsWith("Posted: https://github.test/discussions/1", posted.Out.TrimEnd());
        var sent = Assert.Single(service.Feedback);
        Assert.Equal("The task list repeats a finding\nfor each width.", (string?)sent["text"]);
        Assert.Equal("izzy audit --format tasks", (string?)sent["about"]);

        // A secret is caught on this machine, before the question.
        var secret = Izzy("", "feedback", "The audit stopped with password=s3cr3t!value in my settings", "--yes");
        Assert.Equal(1, secret.Exit);
        Assert.Contains("the post is public", secret.Error);
        Assert.Single(service.Feedback);
    }

    public void Dispose()
    {
        _signing.Dispose();
        Directory.Delete(_home, recursive: true);
    }

    /// <summary>The account service's API, as izzy uses it: the device sign-in approved on the second ask, and the catalog.</summary>
    sealed class Service(ECDsa signing, byte[] bundle) : HttpMessageHandler
    {
        public int TokenRequests { get; private set; }

        public bool Down { get; set; }

        /// <summary>The account reserved the founding price: its key is Pro's, as a preview.</summary>
        public bool Preview { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Down) throw new HttpRequestException("The service is down.");
            var response = request.RequestUri!.AbsolutePath switch
            {
                "/v1/device/start" => Json(HttpStatusCode.OK, new JsonObject
                {
                    ["device_code"] = "device-1", ["user_code"] = "WDJB-MJHT", ["verification_uri"] = "https://account.test/device",
                    ["verification_uri_complete"] = "https://account.test/device?code=WDJB-MJHT", ["expires_in"] = 900, ["interval"] = 5,
                }),
                "/v1/device/token" => ++TokenRequests == 1
                    ? Json(HttpStatusCode.BadRequest, new JsonObject { ["error"] = "authorization_pending" })
                    : Json(HttpStatusCode.OK, new JsonObject { ["key"] = new AccountKey("acct_1", Preview ? ItemTier.Pro : ItemTier.Account, "m_1", "k1", Now, Now.AddDays(30), Preview).Sign(signing) }),
                "/v1/catalog/9.9.9" when request.Headers.Authorization?.Parameter is { Length: > 0 } => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bundle),
                    Headers = { { "X-Izzy-Signature", AccountBundle.Sign(bundle, signing, "k1") } },
                },
                "/v1/logout" => new HttpResponseMessage(HttpStatusCode.NoContent),
                "/v1/feedback" => Posted(request),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
            return Task.FromResult(response);
        }

        /// <summary>What izzy feedback posted.</summary>
        public List<JsonNode> Feedback { get; } = [];

        HttpResponseMessage Posted(HttpRequestMessage request)
        {
            Feedback.Add(JsonNode.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult())!);
            return Json(HttpStatusCode.OK, new JsonObject { ["url"] = "https://github.test/discussions/1" });
        }

        static HttpResponseMessage Json(HttpStatusCode status, JsonObject body) => new(status) { Content = JsonContent.Create(body) };
    }
}
