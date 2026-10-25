# IzzyBlazor

**Rich Blazor components. No circuit required.**

IzzyBlazor is a component library for .NET 10 Blazor Web Apps, built for static SSR. Components render as HTML on the server, gain browser behavior through lightweight custom elements, and support C# bindings and events when you choose an interactive render mode.

Build forms, tables, dialogs, and complete app layouts without requiring a SignalR circuit or WebAssembly runtime for static pages.

[Explore the components](https://izzyblazor.com/docs/components) · [Get started](https://izzyblazor.com/docs/get-started)

![An orders screen built with IzzyBlazor, featuring sidebar navigation, stat cards, search filters, and a sortable, paged table.](https://raw.githubusercontent.com/enabledevmode/izzyblazor/main/docs/images/showcase-light.png)

> **Preview:** `0.1.0-alpha.14`. APIs may change during preview.

## Progressive by design

Start with server-rendered HTML. Add lightweight browser behavior. Bring in C# interactivity where you need it.

The same components support each layer, so you can add interactivity without replacing your UI.

| Component | HTML and CSS | With JavaScript | With Blazor interactivity |
| --- | --- | --- | --- |
| **Dialog** | Opens from a trigger or a `?dialog=id` link, with browser-managed focus trapping | Promotes server-opened dialogs to modal dialogs | Bind open state with `@bind-Open` |
| **Table** | Sort, filter, and page through URL parameters | Select all rows and apply filters as you type | Bind sorting, paging, filters, and selection to C# |
| **Tabs** | Switch panels with radio buttons and CSS | Adds ARIA tab semantics and arrow-key navigation | Bind the selected tab with `@bind-Selected` |
| **Combobox** | Uses a native select that submits with the form | Adds filtering and keyboard navigation | Search on the server as you type |
| **FileUpload** | Submits files through a `Multipart` form as `IFormFile` | Adds drag and drop with file type and size checks | Stream `IBrowserFile` instances through `OnChange` |

## Getting started

Install the package in your server project:

```bash
dotnet add package IzzyBlazor --prerelease
```

Add the stylesheet in `App.razor`:

```razor
@* Inside <head> *@
<link rel="stylesheet" href="@Assets["_content/IzzyBlazor.Core/izzyblazor.css"]" />
```

Add the namespace in `_Imports.razor`:

```razor
@using IzzyBlazor
```

Register the services and map the endpoints in `Program.cs`:

```csharp
builder.Services.AddIzzyBlazor();

// After app.MapRazorComponents<App>():
app.MapIzzyBlazor();
```

IzzyBlazor loads its JavaScript automatically. To display toasts, add `<ToastRegion />` to your layout.

For WebAssembly and Auto render modes, reference `IzzyBlazor.Core` in your `.Client` project and call `AddIzzyBlazorCore()`. See the [WebAssembly and Auto setup guide](https://izzyblazor.com/docs/get-started/webassembly-and-auto) for details.

## Documentation

Visit **[izzyblazor.com](https://izzyblazor.com)** for live component examples, render-mode guidance, and API documentation.

- [Getting started](https://izzyblazor.com/docs/get-started): installation and setup
- [Components](https://izzyblazor.com/docs/components): examples, supported modes, and APIs
- [Style props](https://izzyblazor.com/docs/common/style-props): typed, responsive styling
- [Theming](https://izzyblazor.com/docs/theming): colors, fonts, and theme configuration
- [Source and issues](https://github.com/enabledevmode/izzyblazor): browse the code and report bugs

## Browser support

Requires a modern browser with support for CSS `:has()`, `light-dark()`, and the Popover API.

## License

IzzyBlazor is [MIT licensed](https://github.com/enabledevmode/izzyblazor/blob/main/LICENSE).

Bundled Lucide icons use the ISC license. Brand logos from Tabler Icons and those drawn for IzzyBlazor use the MIT license. The bundled Inter font uses the SIL Open Font License. See [third-party notices](https://github.com/enabledevmode/izzyblazor/blob/main/THIRD-PARTY-NOTICES.md) for details.

IzzyBlazor is an independent project and is not affiliated with Microsoft.
