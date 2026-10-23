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
site/IzzyBlazor.Site/        the documentation site: /docs and /demos (see "The docs site" below)
site/IzzyBlazor.DocsGen/     writes izzyblazor.api.json, the API metadata, from the built library
site/IzzyBlazor.Site.Tests/  checks the registry, every example's frames and the site's links
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
   renders. An element most pages won't have (a table, a combobox, a color picker) loads on
   demand instead: add it to the loaders in `Scripts/lazy.ts` and to `lazyElements` in
   `build/build-assets.mjs`, not to `index.ts`. Every page loads the main module (15 KB gzip
   budget); each on-demand element is its own file (5 KB each). Until its code loads, an
   element shows its server-rendered markup, so it must work without script anyway.
4. **A demo** in `tests/IzzyBlazor.TestHost/Components/Demos/`, plus one line in `DemoCatalog.cs`.
5. **Tests** in `tests/IzzyBlazor.Tests/`, for its behavior (see [Tests](#tests)).
6. **Docs:** an entry in `site/IzzyBlazor.Site/components.json` (or a part on its parent's entry;
   a test fails until it's there), doc comments on every parameter, and `[ModeSupport]` on the
   class saying what works with JavaScript off, with script and on interactive pages (on a
   parameter too, when it differs, such as an `OnClick` that runs only on interactive pages).

### The docs site

`dotnet run --project site/IzzyBlazor.Site` serves the site, built with IzzyBlazor itself and
rendered in static SSR. Its build runs DocsGen, which reads the library's assemblies and XML doc
comments into `izzyblazor.api.json` (also served at `/izzyblazor.api.json`), so API pages,
parameter tables and defaults are never written by hand.

- `components.json` lists each documented component: its id (the URL), category, a one-line
  summary, the parts documented on its page, the mode-support rows for its Overview, its examples
  and its demo scenarios.
- An example is a component in `Examples/{Folder}/{Name}.razor`, known by the id
  `{folder}-{name}` (`Examples/Button/MultiSubmit.razor` is `button-multi-submit`). The viewer
  shows it in three frames of `/frame/{id}?mode=off|static|live`: Off is sandboxed without
  `allow-scripts`, so the browser really runs no script; Live renders it on Blazor Server and loads
  only when picked. Its source is embedded and shown under it; lines between `@* hide *@` and
  `@* /hide *@` are left out. Links in examples open in the whole window with script; in Off they
  open in the frame. An example that opens a dialog, menu or select list sets `"overlay": true`:
  what opens isn't part of the frame's content height, so the frame keeps at least the example's
  `height` and starts the content at the top.
- Each frame starts at its content's height for its width, from `frame-heights.json`, so a page
  doesn't move as its examples load. After adding or changing an example, run the site and
  `npm run measure-frames` (`-- --only <id>` for one; `-- --browser <path>` to pick Chrome or
  Chromium): it loads each example in each mode at frame widths from 256px to 1120px and writes the
  heights where they change. A site test fails on an example with none.
- A component's Overview is `Docs/Overviews/{Name}Overview.razor` when written by hand (see
  Button's); otherwise one is generated from the metadata in the same order: the doc comment's
  summary, its example as Usage, each of the component's examples in `components.json` under its
  title and `lead` (one sentence leading with the prop; backticks mark code), the remarks as Details, its parts, Mode support, Accessibility, Parameters and See also.
  The API page is always generated.
- Accessibility and See also come from the doc comment, for hand-written Overviews too: an
  `<accessibility>` tag (a bullet `<list>`: what the component gives keyboard, screen reader and
  focus users, and what's left to the app) and top-level `<seealso cref="..."/>` tags for related
  components, shown with their one-line summaries. A hand-written Overview renders them with
  `ApiComponent.Accessibility` and `<OverviewSeeAlso />` (and its parts with `<OverviewParts />`),
  as Dialog's does.
- A parameter a component inherits but refuses (Heading's `As`, since `Level` sets its element)
  gets `[RefusesParameter(nameof(As))]` on the component, which keeps it off its pages. The
  component still has to refuse it at run time.
- A component written as a `.razor` file can't carry an XML doc comment, so DocsGen reads its
  header comment (the `@* *@` block after the directives) instead: the text before the first
  markup is its summary, that markup its example, and the rest its remarks. Lines starting `- `
  are a list; markup starts after a blank line or a line ending in `.` or `:`. A line reading
  `Accessibility:` starts the accessibility bullets, and `See also: Select, Combobox.` names
  related components; both go at the end.
- `Api/released.api.json` is the last release's API (names only). The build compares with it to
  mark what each release added ("Added in") and print what it removed; it's refreshed when a
  release is cut (see Releasing).

#### Writing an Overview

An Overview is for using the component; the API page is the reference. Follow Button's:

1. **Usage**: a short code block of the common cases, ready to copy.
2. **A section per thing to do**: a heading, one sentence that leads with the prop ("Use
   `Loading` to show a spinner and block clicks."), then its example. Details such as ARIA
   attributes and edge cases go in the parameter's doc comment or the Accessibility section, not
   the sentence.
3. **Mode support**, **Accessibility** (from the doc comment, as above), **Parameters**
   (`<ParameterTable Compact />`) and **See also**, after the examples.

For each section, pick how to show it by what the reader needs:

| Show | When | Such as |
| --- | --- | --- |
| A preview (`<ExampleViewer />`), source a click away | It looks or behaves differently, or Off · Static · Live changes the result | Sizes, variants, states, interactions, OnClick |
| Code only (`<Code Block>`) | There's something to copy, and a preview would show nothing new | Usage, setup, a pattern to paste, behavior seen only after a post |
| Prose only | Nothing renders | Accessibility, gotchas |

The test: would the preview show something the code doesn't? If not, show the code. Give an
example its own `notes` in `components.json` only for a mode that changes what it does; without
them the viewer shows none.

### Tests

Test the behavior people rely on: what a form posts and binds, what screen readers get (names,
descriptions, states), what works with JavaScript off, what an interactive page receives, and the
messages and limits a user sees. Don't test markup details, class names, attribute values the
script reads, the text of generated CSS, or one-line mappings; the demo and a look in the browser
cover those.

Prefer a few broad tests to many narrow ones; a new case often fits as one more row in an existing
`[Theory]`. When a refactor that doesn't change behavior breaks a test, loosen or delete the test
instead of patching it to match. A test that has to be fixed with every change ends up being fixed
instead of the product, and stops helping.

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

### Naming components

Form controls are named by what they render:

- `...Field`: renders its own label, hint and errors in the field box (`TextField`, `NumberField`;
  later `DateField`, `TimeField`, `SearchField`). Most form controls are fields.
- `...Input`: a bare control with no label, for custom layouts. None today.
- `...Picker`: opens a panel to choose from (`ColorPicker`; a calendar `DatePicker`), so a
  `DateField` and a `DatePicker` differ in whether a panel opens.

`TextArea`, `Select`, `Checkbox`, `Switch`, `Slider` and the groups keep their usual names.
Blazor's own form components are `InputText`, `InputNumber` and so on; don't add names that read as
a reordering of those.

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
| `{Component}{Region}` parts: `CardHeader`, `DialogFooter`, `SidebarBody`… | Regions written among a component's content instead of named fragments, so the content needs no `<ChildContent>` wrapper. Content outside the parts is the body; an optional `{Component}Body` exists where the body needs something (layout props, `Flush`, filling the space). Parts the host draws in its own place (`CardMedia`, `CardHeader`, `CardActions`, `SidebarHeader`, `SidebarFooter`, `DialogHeader`, `BannerActions`, `EmptyStateActions`, `AccordionHeader`, `SiteNavFooter`) derive from `ComponentPart` and don't see a form they're written inside; parts drawn where they're written (`CardBody`, `CardFooter`, `DialogBody`, `DialogFooter`, `SidebarBody`, `AlertActions`) are ordinary components and do | Card, Dialog/Drawer, AppSidebar, Alert, Banner, EmptyState, AccordionItem, SiteNav |

On/off switches are plain `bool`s so they can be written bare (`<Box Grow>`); anything that changes
with screen size is `Responsive<T>`. Two-way state follows Blazor's `@bind-X` pattern (`Open`/
`OpenChanged`, `Selected`/`SelectedChanged`).

Other fixed choices are enums. On a component named like an HTML element (`Button`, `Select`,
`Form`, `Dialog`), avoid naming a parameter after one of that element's attributes when it takes
different values: Rider and ReSharper check `<Button Type="...">` against HTML's `<button type>`
and flag an enum value as wrong. Pick another name, or a bare `bool` for an on/off choice: buttons
take `Submit` rather than `Type`. `Select`'s `Size` (a `ControlSize`, where HTML's `size` is a
number) may break this rule; check it in Rider before 0.1.

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
- tests cover its behavior (not its markup; see [Tests](#tests)), and the docs in
  `docs/components.md` cover it.

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

In the same commit, refresh the docs site's record of the release, so the next one can tell what's
new. Build the site with the new version, then write the file from the old one:

```
dotnet build site/IzzyBlazor.Site -c Release
dotnet site/IzzyBlazor.DocsGen/bin/Release/net10.0/IzzyBlazor.DocsGen.dll --source src --out site/IzzyBlazor.Site/obj/Release/izzyblazor.api.json --previous site/IzzyBlazor.Site/Api/released.api.json --baseline site/IzzyBlazor.Site/Api/released.api.json
```

The build lists what was removed since the last release ("Removed since ..."); put those in the
release notes too.

For release notes, add `docs/releases/v<version>.md` in the same commit: it heads the GitHub
release's notes, above the generated list of commits. List every rename, since Razor takes an old
parameter name silently. Write each paragraph and list item on one line, without wrapping it: GitHub keeps every line
break in a release body, so wrapped text shows gaps mid-sentence.

The Release workflow (`.github/workflows/release.yml`) checks that the tag is on main and matches
`<Version>`, builds, tests, checks the asset budgets and packs with read-only access; a second job
pushes `IzzyBlazor.Core` then `IzzyBlazor` (with their symbol packages) to nuget.org, then creates
a GitHub release with the packages attached (marked as a pre-release for versions like `-alpha.1`). It uses nuget.org trusted publishing: no API key is
stored; `NuGet/login` exchanges the job's GitHub token for a key that lasts an hour. It runs in the
`release` environment, and needs the `NUGET_USER` secret (the nuget.org username of the policy's
creator). A failed run can be rerun: packages already on nuget.org are skipped. A published version
can't be replaced, only unlisted, so a fix needs a new version.
