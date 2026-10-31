using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

internal static class LocatorExtensions
{
    /// <summary>
    /// Scrolls it into view, finding it again if the circuit connecting replaces it on the way, which
    /// Playwright retries for a click but not for this. On a slow machine that's often.
    /// </summary>
    public static async Task ScrollIntoViewSettledAsync(this ILocator locator)
    {
        for (var tries = 5; ; tries--)
        {
            try
            {
                await locator.ScrollIntoViewIfNeededAsync();
                return;
            }
            catch (PlaywrightException e) when (tries > 1 && e.Message.Contains("not attached", StringComparison.Ordinal))
            {
            }
        }
    }
}
