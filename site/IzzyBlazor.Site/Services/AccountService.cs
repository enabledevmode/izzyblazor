using System.Net.Http.Json;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// The account service (account.izzyblazor.com), as far as this site needs it: the pricing page hands it an
/// address that reserves the founding price. <c>Account:Service</c> names another one, such as a local copy.
/// </summary>
public sealed class AccountService(HttpClient http, ILogger<AccountService> logger)
{
    public const string Address = "https://account.izzyblazor.com";

    /// <summary>Where a free account is made: the sign-in page, since a first sign-in makes one.</summary>
    public const string SignIn = Address + "/signin";

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
}
