using System.Net;
using System.Text.Json.Nodes;
using IzzyBlazor.Cli;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// izzy and a newer release: which version counts as newer, the line that says so once a day, and izzy update
/// moving an app's package and its own izzy to it. nuget.org is a stand-in.
/// </summary>
public sealed class UpdateTests : IDisposable
{
    static readonly DateTimeOffset Now = new(2026, 12, 1, 12, 0, 0, TimeSpan.Zero);

    readonly string _work = Directory.CreateTempSubdirectory("izzy-update-").FullName;

    sealed class NuGet(params string[] versions) : HttpMessageHandler
    {
        public int Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked++;
            Assert.Equal(Releases.Index, request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new JsonObject { ["versions"] = new JsonArray([.. versions.Select(v => (JsonNode)v)]) }.ToJsonString()) });
        }
    }

    [Fact]
    public void The_newest_release_is_by_number_and_a_stable_izzy_is_offered_no_prerelease()
    {
        string[] versions = ["0.1.0-alpha.9", "0.1.0-alpha.10", "0.1.0-alpha.2", "0.1.0", "0.2.0-beta.1"];

        Assert.Equal("0.2.0-beta.1", Releases.Newest(versions, "0.1.0-alpha.9"));
        Assert.Equal("0.1.0-alpha.10", Releases.Newest(versions[..3], "0.1.0-alpha.9"));
        Assert.Equal("0.1.0", Releases.Newest(versions, "0.0.9"));
        Assert.Null(Releases.Newest(versions, "0.1.0"));
    }

    [Fact]
    public void A_newer_release_is_said_once_a_day_and_nuget_is_asked_once_a_day()
    {
        var nuget = new NuGet("0.1.0-alpha.29", "0.1.0-alpha.30");
        var now = Now;
        string? Run()
        {
            var releases = new Releases(_work, "0.1.0-alpha.29", nuget, () => now);
            releases.Begin();
            // A command long enough for the check to finish beside it.
            releases.Check?.Wait();
            return releases.Notice("dotnet izzy");
        }

        Assert.Equal("IzzyBlazor 0.1.0-alpha.30 is out (this is 0.1.0-alpha.29). To update: dotnet izzy update", Run());
        now = Now.AddHours(2);
        Assert.Null(Run());
        Assert.Equal(1, nuget.Asked);

        now = Now.AddHours(25);
        Assert.NotNull(Run());
        Assert.Equal(2, nuget.Asked);
    }

    [Fact]
    public void Update_moves_the_apps_package_and_its_izzy_to_the_newest_release()
    {
        var app = Path.Combine(_work, "Acme");
        Directory.CreateDirectory(Path.Combine(app, ".config"));
        File.WriteAllText(Path.Combine(app, "Acme.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="IzzyBlazor" Version="0.1.0-alpha.20" />
                <PackageReference Include="Other" Version="0.1.0-alpha.20" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(app, ".config", "dotnet-tools.json"), ToolManifest.New("0.1.0-alpha.20"));
        (int Exit, string Out) Izzy(params string[] args)
        {
            var output = new StringWriter();
            var releases = new Releases(_work, Generator.IzzyBlazorVersion, new NuGet("0.1.0-alpha.20", "99.0.0-alpha.1"), () => Now);
            return (new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), app, output, new StringWriter(), releases: releases).Run(args), output.ToString());
        }

        var preview = Izzy("update", "--dry-run", "--json");
        Assert.Equal(0, preview.Exit);
        var json = JsonNode.Parse(preview.Out)!;
        Assert.Equal(("0.1.0-alpha.20", "99.0.0-alpha.1", false), ((string)json["from"]!, (string)json["to"]!, (bool)json["written"]!));
        Assert.Equal(["Acme.csproj", ".config/dotnet-tools.json"], json["inserts"]!.AsArray().Select(i => (string)i!["path"]!));

        // Written, without the manifest: restoring the tool and building are dotnet's, and need the release to exist.
        File.Delete(Path.Combine(app, ".config", "dotnet-tools.json"));
        Assert.Equal(0, Izzy("update", "--no-build").Exit);
        var csproj = File.ReadAllText(Path.Combine(app, "Acme.csproj"));
        Assert.Contains("Include=\"IzzyBlazor\" Version=\"99.0.0-alpha.1\"", csproj);
        Assert.Contains("Include=\"Other\" Version=\"0.1.0-alpha.20\"", csproj);
        Assert.Contains("Nothing to change", Izzy("update", "--no-build").Out);
    }

    public void Dispose() => Directory.Delete(_work, recursive: true);
}
