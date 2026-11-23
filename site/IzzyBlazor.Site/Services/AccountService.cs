using System.Net.Http.Json;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// The account service (account.izzyblazor.com), as far as this site needs it: the pricing page hands it an
/// address that reserves the founding price, and the newsletter form one that subscribes. <c>Account:Service</c> names another one, such as a local copy.
/// </summary>
public sealed class AccountService(HttpClient http, ILogger<AccountService> logger)
{
    public const string Address = "https://account.izzyblazor.com";

    /// <summary>Where a free account is made: the sign-in page, since a first sign-in makes one.</summary>
    public const string SignIn = Address + "/signin";

    /// <summary>Where the header's Sign out posts: the service signs the browser out and comes back here.</summary>
    public const string SignOut = Address + "/signout/site";

    /// <summary>
    /// The cookie the account service sets on the domain the two sites share while a browser is signed in:
    /// the email and name to show, each escaped, with a | between. It grants nothing; this site only shows it.
    /// </summary>
    public const string SignedInCookie = "ib-account";

    /// <summary>The account the browser says it's signed in to, for the header; null when it isn't, or the cookie isn't one of ours.</summary>
    public static SignedInAccount? SignedIn(HttpRequest? request)
    {
        if (request?.Cookies[SignedInCookie] is not { Length: > 0 and <= 1024 } value || value.Split('|') is not [var email, var name]) return null;
        try
        {
            email = Uri.UnescapeDataString(email).Trim();
            name = Uri.UnescapeDataString(name).Trim();
        }
        catch (UriFormatException)
        {
            return null;
        }

        return email.Length is 0 or > 254 || !email.Contains('@') || name.Length > 100 ? null : new SignedInAccount(email, name.Length == 0 ? null : name);
    }

    /// <summary>Where a subscriber changes or ends the newsletter: the account's email settings.</summary>
    public const string Email = Address + "/email";

    /// <summary>Reserves the founding price for <paramref name="email"/>. False when the service refused or couldn't be reached.</summary>
    public async Task<bool> ReserveAsync(string email, CancellationToken cancel = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("v1/reservations", new { email }, cancel);
            if (!response.IsSuccessStatusCode) logger.LogWarning("The account service answered a reservation with {Status}.", (int)response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(e, "The account service could not be reached for a reservation.");
            return false;
        }
    }

    /// <summary>
    /// Subscribes <paramref name="email"/> to the newsletter: the service sends it a link that confirms, unless it's
    /// subscribed already. <paramref name="source"/> says which form it came from.
    /// </summary>
    public async Task<Subscription> SubscribeAsync(string email, string source, CancellationToken cancel = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("v1/subscriptions", new { email, source }, cancel);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("The account service answered a subscription with {Status}.", (int)response.StatusCode);
                return Subscription.Failed;
            }

            var answer = await response.Content.ReadFromJsonAsync<SubscriptionAnswer>(cancel);
            return answer?.Status == "subscribed" ? Subscription.Already : Subscription.Sent;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(e, "The account service could not be reached for a subscription.");
            return Subscription.Failed;
        }
    }

    private sealed record SubscriptionAnswer(string? Status);
}

/// <summary>What the account service did with a newsletter address.</summary>
public enum Subscription
{
    /// <summary>It sent the link that confirms.</summary>
    Sent,

    /// <summary>The address is subscribed already; nothing was sent.</summary>
    Already,

    /// <summary>The service refused or couldn't be reached.</summary>
    Failed,
}

/// <summary>The account a browser is signed in to at the account service, as its cookie names it.</summary>
public sealed record SignedInAccount(string Email, string? Name)
{
    /// <summary>The name, or the email where the account has none.</summary>
    public string Display => Name ?? Email;
}
