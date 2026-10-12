# Getting started

IzzyBlazor targets .NET 10 Blazor Web Apps. It works on static server-rendered pages, with Blazor's
enhanced navigation and form handling, and on Interactive Server, WebAssembly and Auto pages.

## Setup

1. Install the `IzzyBlazor` package in your server project (or reference `src/IzzyBlazor` while
   developing the library itself):

   ```bash
   dotnet add package IzzyBlazor --prerelease
   ```

2. Add the stylesheet to `App.razor`:

   ```html
   <link rel="stylesheet" href="@Assets["_content/IzzyBlazor.Core/izzyblazor.css"]" />
   ```

3. Add `@using IzzyBlazor` to `_Imports.razor`.

4. Register the services in `Program.cs`, and put `<ToastRegion />` in your layout if you use toasts:

   ```csharp
   builder.Services.AddIzzyBlazor();
   ```

   This enables toasts, toasts that survive a redirect, and AppSidebar's remembered collapsed state.
   Other components need no registration.

The JavaScript loads itself: Blazor picks up `IzzyBlazor.Core.lib.module.js` as a JS initializer, so
there's no script tag to add.

## Packages

IzzyBlazor ships as two packages:

- **`IzzyBlazor`** (what you install): everything, for server projects. It adds the pieces that read
  and write the request's cookies.
- **`IzzyBlazor.Core`**: every component, the CSS and the JS, with no dependency on the ASP.NET Core
  server, so it also runs in the browser. `IzzyBlazor` includes it.

## WebAssembly and Auto render modes

Components on `InteractiveWebAssembly` or `InteractiveAuto` pages live in the `.Client` project,
which can't reference the server framework. Reference `IzzyBlazor.Core` there and call
`builder.Services.AddIzzyBlazorCore();` in its `Program.cs`. The server project keeps `IzzyBlazor`
and `AddIzzyBlazor()`.

In the browser, toasts are added straight away (there's no redirect to carry them over), and
AppSidebar's remembered state arrives from the prerender through persisted state.

## Forms on static pages

`Form` wraps Blazor's `EditForm`. Pair it with `[SupplyParameterFromForm]` on the model property so
the same form posts and validates on a static page:

```razor
<Form Model="Signup" FormName="signup" OnValidSubmit="SaveAsync">
    <FormErrors />
    <TextField @bind-Value="Signup.Email" />
    <Select @bind-Value="Signup.Plan" Placeholder="Choose a plan" />
    <Button Type="ButtonType.Submit">Create account</Button>
</Form>

@code {
    [SupplyParameterFromForm(FormName = "signup")] public SignupModel? Signup { get; set; }
    protected override void OnInitialized() => Signup ??= new();
}
```

## Next steps

- [Styling](styling.md): style props, responsive values, colors and themes
- [Components](components.md): what each component does in each mode
