using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Site.Examples.ErrorGuard;

/// <summary>
/// The dashboard's customer list, from a CRM that's down: loading it throws, as an HttpClient call
/// to a service answering 503 would.
/// </summary>
public sealed class TopCustomers : ComponentBase
{
    protected override async Task OnInitializedAsync()
    {
        await Task.Yield();
        throw new HttpRequestException("The CRM didn't answer: 503 Service Unavailable.");
    }
}
