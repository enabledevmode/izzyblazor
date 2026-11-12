using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
#if (UseProgramMain)

namespace {{RootNamespace}}.Client;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Services.AddIzzyBlazorCore();
#if (IndividualLocalAuth)
        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthenticationStateDeserialization();
#endif

        await builder.Build().RunAsync();
    }
}
#else

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddIzzyBlazorCore();
#if (IndividualLocalAuth)
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();
#endif

await builder.Build().RunAsync();
#endif
