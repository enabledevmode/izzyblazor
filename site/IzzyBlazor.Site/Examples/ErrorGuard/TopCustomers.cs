using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Site.Examples.ErrorGuard;

/// <summary>
/// The customers who spent most, from a CRM that's down: loading them throws, as a call to a service
/// answering 503 would.
/// </summary>
public sealed class TopCustomers : ComponentBase
{
    protected override async Task OnInitializedAsync()
    {
        await Task.Yield();
        throw new HttpRequestException("The CRM didn't answer: 503 Service Unavailable.");
    }
}
