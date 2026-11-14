using System.Net;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Site.Tests;

/// <summary>Form's honeypot, posted as a bot posts: no browser, the hidden field filled.</summary>
public sealed partial class FormHoneypotTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task A_bot_that_fills_the_honeypot_gets_the_same_answer_and_isnt_saved()
    {
        using var client = new HttpClient(new HttpClientHandler()) { BaseAddress = site.Client.BaseAddress };
        var page = await client.GetStringAsync("frame/form-honeypot?mode=static");
        var before = Count(page);

        var bot = await PostAsync(client, page, $"{Guid.NewGuid():N}@example.com", honeypot: "https://spam.example");
        Assert.Contains("on the list", bot);
        Assert.Equal(before, Count(bot));

        var person = await PostAsync(client, page, $"{Guid.NewGuid():N}@example.com", honeypot: "");
        Assert.Contains("on the list", person);
        Assert.Equal(before + 1, Count(person));
    }

    private static async Task<string> PostAsync(HttpClient client, string page, string email, string honeypot)
    {
        var fields = new Dictionary<string, string>
        {
            ["_handler"] = "form-waitlist",
            ["__RequestVerificationToken"] = Token().Match(page).Groups[1].Value,
            [EmailName().Match(page).Groups[1].Value] = email,
            ["ib-hp"] = honeypot,
        };
        var response = await client.PostAsync("frame/form-honeypot?mode=static", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static int Count(string html) => int.Parse(Waiting().Match(html).Groups[1].Value.Replace(",", ""));

    [GeneratedRegex(@"name=""__RequestVerificationToken"" value=""([^""]+)""")]
    private static partial Regex Token();

    [GeneratedRegex(@"name=""([^""]*Email)""")]
    private static partial Regex EmailName();

    [GeneratedRegex(@"([\d,]+) people are waiting")]
    private static partial Regex Waiting();
}
