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

   Optionally, set the preset and your brand color in the same `<head>` (see
   [Styling](styling.md#presets-and-colors)):

   ```razor
   <IzzyTheme Preset="ThemePreset.Standard" Primary="#0a7d5a" />
   ```

3. Add `@using IzzyBlazor` to `_Imports.razor`. Prefer this to a `global using`: it brings the
   components into `.razor` files only, so names such as `Table` and `Column` don't meet EF Core's
   `[Table]` and `[Column]` attributes in your entity classes.

4. Register the services in `Program.cs`:

   ```csharp
   builder.Services.AddIzzyBlazor();
   ```

   This adds `ToastService` and `DialogService`, carries toasts over a redirect, and lets the server
   read IzzyBlazor's cookies (through `IRequestCookies`): AppSidebar's collapsed state, a dismissed
   `Banner` with a `RememberKey`, and the scheme chosen with `ColorSchemeSwitch`, which `IzzyTheme`
   applies. Other components need no registration.

   If you use toasts, put `<ToastRegion />` in your layout. If you open dialogs from code with
   `DialogService`, put one `<DialogHost />` on the interactive page or layout (see
   [Overlays](components.md#overlays)).

5. Map IzzyBlazor's endpoints after your components, for `ColorSchemeSwitch` with JavaScript off:

   ```csharp
   app.MapRazorComponents<App>();
   app.MapIzzyBlazor();
   ```

The JavaScript loads itself: Blazor picks up `IzzyBlazor.Core.lib.module.js` as a JS initializer, so
there's no script tag to add.

## Name clashes

IzzyBlazor's components have plain names (`Button`, `Card`, `Table`). If a file also sees a type of
yours with the same name, such as your own `Status` enum, the compiler reports an ambiguous
reference; qualify one of them (`IzzyBlazor.Status.Success`) or add an alias in `_Imports.razor`
(`@using Status = IzzyBlazor.Status`).

## Other languages

Components label their buttons and announce states in English. To change that app-wide, register an
`IzzyText` with your strings, or a subclass that reads your resources (see
[Text and other languages](components.md#text-and-other-languages)).

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
AppSidebar's remembered state and ColorSchemeSwitch's choice arrive from the prerender through
persisted state.

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
