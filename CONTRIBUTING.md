# Contributing

IzzyBlazor doesn't take pull requests for now; please open an
[issue](https://github.com/enabledevmode/izzyblazor/issues) for a bug or an idea. This guide is how
the library is built: the layout, the dev host, the naming rules and what "done" means.

## Repository layout

```
src/IzzyBlazor.Core/         the components, CSS and JS; runs in any render mode (IzzyBlazor.Core package)
  Core/                      base classes, responsive values, style mapping
  Components/                components, one folder per group
  Styles/                    tokens, base and component CSS (bundled by esbuild)
  Scripts/                   custom elements in TypeScript (bundled by esbuild)
src/IzzyBlazor/              server-only pieces: cookie-backed toast carrier and request cookies (IzzyBlazor package)
tests/IzzyBlazor.TestHost/   dev host: every demo at /static/{demo} and /interactive/{demo}, previews at /preview/...
tests/IzzyBlazor.Tests/      xUnit and bUnit tests
build/                       asset build and dev scripts
docs/                        documentation and README images
```

`IzzyBlazor.Core` can't reference the ASP.NET Core server framework, so it also runs in WebAssembly.
Anything that needs the request (`HttpContext`, cookies, `QueryHelpers`) goes in `src/IzzyBlazor`
behind an interface defined in Core. Core's build fails if server types creep in.

## Developing

Requirements: .NET 10 SDK and Node 20 or newer.

```bash
npm ci        # once: installs esbuild, the Lucide source icons and the Inter font
npm run dev   # asset watcher + dotnet watch on the dev host
```

Open the host (http://localhost:5180 by default) and pick a component. Use the header to switch
between **Static** and **Interactive**, and between light and dark themes.

To test with JavaScript off: open a static page, press Ctrl+Shift+P (Cmd+Shift+P on macOS) in Chrome
or Edge DevTools, run **Disable JavaScript**, and reload. A "JavaScript off" note appears next to the page
title.

Other commands:

```bash
dotnet build              # also builds CSS and JS (runs npm ci the first time)
dotnet test               # unit and render tests
npm run build             # assets only, minified
```

The README screenshots come from the dev host's `/preview/showcase` page, at 1280×860 with a device
scale factor of 2, in light and dark. The other full-page previews are `/preview/app-shell`,
`/preview/app-shell-live` (interactive) and `/preview/layout/{kind}` (realistic screens such as
`settings`, `list` and `dashboard`).

## Adding a component

1. **The class.** Add it under `src/IzzyBlazor.Core/Components/<Group>/`, in the `IzzyBlazor`
   namespace (`@namespace IzzyBlazor` or `namespace IzzyBlazor;`, not one per folder), inheriting
   `StyledComponentBase` (boxes and surfaces: item and surface props), `LayoutItemBase` (anything
   that sits in a layout: item props only), or `IzzyComponentBase` (parts a parent always lays out,
   such as a tab or menu item). Override `BuildAttributes`: add the base class first, call
   `base.BuildAttributes`, then apply component parameters with `ApplyResponsive`. Use
   `Responsive<T>` for any parameter that should vary by breakpoint. Put `Class`, `Style` and the
   style props on the outermost element, the one a parent lays out.
2. **The CSS.** Add it under `src/IzzyBlazor.Core/Styles/components/` inside
   `@layer izzy.components`, and import it in `Styles/izzyblazor.css`. New responsive props also need
   an entry in `STYLE_PROPS` in `build/build-assets.mjs`.
3. **Browser behavior,** if it needs any: add a custom element under
   `src/IzzyBlazor.Core/Scripts/elements/` extending `IzzyElement`, and import it in
   `Scripts/index.ts`. Elements change attributes, classes and ARIA state, never content Blazor
   renders.
4. **A demo** in `tests/IzzyBlazor.TestHost/Components/Demos/`, plus one line in `DemoCatalog.cs`.
5. **Tests** in `tests/IzzyBlazor.Tests/`.

### Namespaces

The public API is one namespace, `IzzyBlazor`, so `@using IzzyBlazor` in `_Imports.razor` brings in
every component, enum and value type. Folders organize the source, not the API: a type keeps its
namespace when its file moves, and users never need to know which package (`IzzyBlazor` or
`IzzyBlazor.Core`) it ships in. Both library projects have the root namespace `IzzyBlazor`
(`IzzyBlazor.Core` sets `RootNamespace`), and `.editorconfig` turns off the IDE's
namespace-matches-folder check for `src/`.

- Library types: `IzzyBlazor`.
- A sub-namespace only for opt-in extras that shouldn't be in scope by default, such as
  `IzzyBlazor.Units` (`14.Rem`).
- Service registration (`AddIzzyBlazor`, `AddIzzyBlazorCore`):
  `Microsoft.Extensions.DependencyInjection`, as .NET libraries do.
- Tests and the dev host follow their folders (`IzzyBlazor.Tests`, `IzzyBlazor.TestHost.*`).

### Naming parameters

Use the same name for the same idea everywhere:

| Name | Means | Examples |
| --- | --- | --- |
| `Label` | Names the component or control itself: visible text on a field or trigger, or the accessible name when nothing is visible | field labels, `Menu Label`, `Tab Label`, `Pagination Label` |
| `Title` | The heading of a section or panel | Card, Dialog, Alert, Popover panel, AccordionItem, table column |
| `...Label` suffix | Text only screen readers get (`aria-label`, visually hidden) | `CloseLabel`, `PreviousLabel`, `LoadingLabel` |
| `...Text` suffix | Visible text | `EmptyText`, `ApplyText`, `ClearText` |
| `Icon` | One fixed icon slot | `NavItem Icon`, `Tab Icon` |
| `StartIcon`, `EndIcon` | An icon beside text that could go on either side | `Button`, `TextField`; `Badge StartIcon` |
| `Accent` | A color set: a fill plus the text that goes with it (on the fill, as a tint, as text). The roles `Primary`, `Neutral`, `Danger`, or a custom `Accent.Of(css)` / `Accent.Token(name)`. On feedback it replaces the status's color | Button, MenuItem, Progress, Avatar; `Badge Accent`, `Banner Accent` |
| `Status` | Feedback meaning: `Info`, `Success`, `Warning`, `Danger`, `Neutral`; also sets icons and what screen readers hear | Alert, Badge, Toast |
| `Color`, `Bg` | Text (or stroke) color and a surface's background, as in CSS. When a color brings its own text color, it's an `Accent` | Box, TextBlock, Icon, Spinner, Image |
| `ColorScheme` | `Auto`, `Light`, `Dark`, `Inverse` (parts only) | AppSidebar, Box, `IzzyTheme ColorScheme` |
| `Items`, `ItemText`, `ItemTemplate` | A list of choices | Select, RadioGroup, Combobox, FilterSelect |
| `For` | The id of the element a control acts on | `DialogTrigger For`, `SidebarToggle For` |
| `FullWidth` | Fill the available width | Button, Tabs |
| `Size` | `ControlSize` for anything that lines up with buttons and fields | Button, fields, Tabs, Table |
| `Header`, `Footer`, `Actions` | Render fragments for those regions | Card (all three), Dialog and AppSidebar (`Header`, `Footer`), Alert (`Actions`) |

On/off switches are plain `bool`s so they can be written bare (`<Box Grow>`); anything that changes
with screen size is `Responsive<T>`. Two-way state follows Blazor's `@bind-X` pattern (`Open`/
`OpenChanged`, `Selected`/`SelectedChanged`).

### Definition of done

A component is done when:

- it renders complete, working HTML with JavaScript off, and its demo works that way;
- it's better with JavaScript on, and nothing breaks after enhanced navigation;
- on interactive pages, its state binds to C# (`@bind-...`) where it has any;
- it's keyboard accessible, with correct ARIA, and readable in light and dark themes;
- it works at phone width;
- inline components (links, code, badges, keys) keep their natural width as flex or grid items: a
  `Column` stretches its items, so give them `width: fit-content`;
- at 100% zoom its edges, icons and offsets land on whole pixels: small fixed graphics (icons,
  dots, checkboxes) have whole, even sizes and sit at whole-pixel offsets in the box they're
  centered in, and its heights don't add half pixels to the page. Use `round()` where a size comes
  from rem or a percentage, and center even graphics in even spaces;
- it has render tests, and the docs in `docs/components.md` cover it.

## Commits

One logical change per commit, with messages in the form `Area: what changed`, for example
`Combobox: keep focus in the input after a click`.

## Releasing

Pushing a version tag publishes both packages. Set `<Version>` in `Directory.Build.props`, commit it
to main, then tag that commit with the same version:

```
git tag v0.1.0-alpha.1
git push origin v0.1.0-alpha.1
```

For release notes, add `docs/releases/v<version>.md` in the same commit: it heads the GitHub
release's notes, above the generated list of commits. List every rename, since Razor takes an old
parameter name silently.

The Release workflow (`.github/workflows/release.yml`) checks that the tag is on main and matches
`<Version>`, builds, tests, checks the asset budgets and packs with read-only access; a second job
pushes `IzzyBlazor.Core` then `IzzyBlazor` (with their symbol packages) to nuget.org, then creates
a GitHub release with the packages attached (marked as a pre-release for versions like `-alpha.1`). It uses nuget.org trusted publishing: no API key is
stored; `NuGet/login` exchanges the job's GitHub token for a key that lasts an hour. It runs in the
`release` environment, and needs the `NUGET_USER` secret (the nuget.org username of the policy's
creator). A failed run can be rerun: packages already on nuget.org are skipped. A published version
can't be replaced, only unlisted, so a fix needs a new version.
