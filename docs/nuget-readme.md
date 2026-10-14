# IzzyBlazor

**Blazor components. No circuit required.** Works in static SSR. No SignalR or WebAssembly needed.

IzzyBlazor is a component library for .NET 10 Blazor Web Apps, built for static server rendering
first. Every component renders complete, working HTML on the server, even with JavaScript off, gets
better when scripts run, and binds to C# on interactive pages.

![An orders screen built with IzzyBlazor: app shell with sidebar navigation, stat cards, a search-and-filter bar and a sortable, paged table.](https://raw.githubusercontent.com/enabledevmode/izzyblazor/main/docs/images/showcase-light.png)

> **Preview** (`0.1.0-alpha.1`). The API will change before 0.1.

## Every component works three ways

| | With JavaScript off | With JavaScript | On interactive pages |
| --- | --- | --- | --- |
| **How** | Plain HTML, forms, links and CSS | Small custom elements add polish | Blazor events keep C# in sync |
| **Dialog** | Opens from a link (`?dialog=id`), closes with a form | Focus handling, backdrop click | `@bind-Open` |
| **Table** | Sort and page through links in the URL | Same, without full reloads | Navigates through Blazor |
| **Tabs** | Radio buttons and CSS switch panels | Proper ARIA tabs, roving focus | `@bind-Selected` |
| **Combobox** | A native select that posts with the form | Type to filter, keyboard navigation | Server-side search as you type |
| **FileUpload** | The file input posts with the form (`IFormFile`) | Drop files anywhere on the zone; type and size checked first | `OnChange` streams `IBrowserFile`s |

## Getting started

```bash
dotnet add package IzzyBlazor --prerelease
```

```razor
@* App.razor, in <head> *@
<link rel="stylesheet" href="@Assets["_content/IzzyBlazor.Core/izzyblazor.css"]" />

@* _Imports.razor *@
@using IzzyBlazor
```

```csharp
// Program.cs
builder.Services.AddIzzyBlazor();
// after app.MapRazorComponents<App>():
app.MapIzzyBlazor();
```

The JavaScript loads itself. For toasts, add `<ToastRegion />` to your layout. `IzzyBlazor` is for
server projects; WebAssembly (`.Client`) projects reference `IzzyBlazor.Core` and call
`AddIzzyBlazorCore()`.

## Documentation

- [Getting started](https://github.com/enabledevmode/izzyblazor/blob/main/docs/getting-started.md)
- [Components](https://github.com/enabledevmode/izzyblazor/blob/main/docs/components.md)
- [Styling](https://github.com/enabledevmode/izzyblazor/blob/main/docs/styling.md)
- [Source and issues](https://github.com/enabledevmode/izzyblazor)

[MIT licensed](https://github.com/enabledevmode/izzyblazor/blob/main/LICENSE). Bundled Lucide icons are ISC
licensed and the brand logos come from Simple Icons (CC0); see
[THIRD-PARTY-NOTICES.md](https://github.com/enabledevmode/izzyblazor/blob/main/THIRD-PARTY-NOTICES.md).
IzzyBlazor is an independent project and is not affiliated with Microsoft.
