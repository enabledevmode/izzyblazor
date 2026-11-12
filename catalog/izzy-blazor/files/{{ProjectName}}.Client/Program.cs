using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
#if (UseProgramMain)

namespace {{RootNamespace}}.Client;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Services.AddIzzyBlazorCore();

        await builder.Build().RunAsync();
    }
}
#else

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddIzzyBlazorCore();

await builder.Build().RunAsync();
#endif
