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
tests/IzzyBlazor.TestHost/   dev host: full-page previews at /preview/... (the README screenshot, shell layouts)
tests/IzzyBlazor.Tests/      xUnit and bUnit tests
site/IzzyBlazor.Site/        the documentation site at izzyblazor.com: /docs and /demos (see "The docs site" below)
site/IzzyBlazor.DocsGen/     writes izzyblazor.api.json, the API metadata, from the built library
site/IzzyBlazor.Site.Tests/  checks the registry, every example in each mode and the site's links
build/                       asset build and dev scripts
docs/                        documentation, release notes (docs/releases) and README images
Dockerfile, fly.toml         the docs site's image and its Fly.io app (see "Hosting")
```

`IzzyBlazor.Core` can't reference the ASP.NET Core server framework, so it also runs in WebAssembly.
Anything that needs the request (`HttpContext`, cookies, `QueryHelpers`) goes in `src/IzzyBlazor`
behind an interface defined in Core. Core's build fails if server types creep in.

## Developing

Requirements: .NET 10 SDK and Node 20 or newer.

```bash
npm ci        # once: installs esbuild, the Lucide source icons and the Inter font
npm run dev   # asset watcher + dotnet watch on the docs site
```

Open the docs site (http://localhost:5190) and pick a component. Its examples switch between
**Off** (JavaScript off), **Static** and **Live** (interactive), and the header between light and
dark.

Other commands:

```bash
dotnet build              # also builds CSS and JS (runs npm ci the first time)
dotnet test               # unit, render and site tests (one site test runs in Chromium)
npm run build             # assets only, minified
```

The dev host (`tests/IzzyBlazor.TestHost`, http://localhost:5180) holds full-page previews. The
README screenshots come from its `/preview/showcase` page, at 1280×860 with a device
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
   renders. The one exception is `<ib-calendar>` and `<ib-period-grid>`, which rewrite their cells to change page: C#
   keeps its first render of them fixed, so Blazor's diff never writes there, and what can change
   reaches the script in attributes on the element. An element most pages won't have (a table, a combobox, a color picker) loads on
   demand instead: add it to the loaders in `Scripts/lazy.ts` and to `lazyElements` in
   `build/build-assets.mjs`, not to `index.ts`. Every page loads the main module (15 KB gzip
   budget); each on-demand element is its own file (6 KB each). Its server-rendered markup must
   work without script: with JavaScript off, and where its code can't load (lazy.ts then marks
   it `data-ib-failed`, and its CSS lays it out as with no script).
   **The script adds behavior, never layout.** IzzyBlazor's script arrives after the page first
   paints, and an on-demand element later still, so anything whose size differs with script (a
   copy button, a hex box, a chip box in place of a list box, no Apply button) is decided in CSS
   by `@media (scripting: enabled)` and `(scripting: none)`, which the browser knows at the
   first paint, not by `:defined` or `[data-ready]`, and drawn by the server, not added by the
   script. Until the script arrives those parts are visible but inert; anything that would take
   keystrokes invisibly is hidden until ready (MultiSelect's list box). A swap that changes no
   size (Combobox's select for its input) can wait for `[data-ready]`, so the working control
   shows until then. Likewise, a layout choice made on `IsInteractive` uses `WillBeInteractive`
   where a prerender would otherwise be drawn another size (Table's filter Apply row, Tag's
   remove button), keeping links that work before the circuit connects. A site test fails on a
   `:defined` rule. Toolbar is the one place the script decides layout, since only it can measure
   what fits: the server writes container queries from widths it estimates (button labels in
   Inter, `build/measure-text.mjs`) for the first paint, and the script, which sets
   `[data-ib-measured]`, keeps that layout on its first pass when it fits and the measurement
   differs by no more than a few pixels.
4. **Examples** on the docs site: one file per example in `site/IzzyBlazor.Site/Examples/{Component}/`,
   listed on its `components.json` entry (see [The docs site](#the-docs-site)).
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
  renders it into the page itself, in the page's mode (the Off · Static · Live switch, kept in
  `?mode=` and a cookie): Static as part of the page; Live as an interactive island, prerendered
  with the page; Off on a page that sends no Blazor and no IzzyBlazor script and reads the
  library's stylesheet with its `@media (scripting: ...)` rules as JavaScript off would. So an
  example arrives with the page at its own size, with nothing to measure. Its source is embedded
  and shown under it; lines between `@* hide *@` and `@* /hide *@` are left out.
- Examples on a page share it, so give each its own ids, `FormName` and keys in the address: two
  examples reading `?page=` would page together (use `QueryKey`, `QueryPrefix` or a field's
  `Name`). Site tests check ids and address keys on every page. A link or form in an example that
  loads the page again brings the reader back to that example (`site.js`). An app-wide part such
  as `ToastRegion` is in the docs layout, not in an example.
- A demo scenario (`"page": true`) is a page of its own, sized by the window: it shows in a frame
  of `/frame/{id}?mode=` that is `height` pixels tall and scrolls inside, with a phone width.
  Off frames are sandboxed without `allow-scripts`; links in them open in the frame.
  Make it look like a real page, not a fragment in the frame's corner: an `AppShell` with an
  `AppHeader` (a made-up brand such as "Acme"), the content centered (`ContentWidth`) or padded,
  cards on `ShellVariant.Canvas` where there are cards, and one h1 (card titles one level
  under it, `TitleLevel="2"`). Set `height` to the page's tallest state on desktop: a form after
  a submit with errors, and after a valid one with its message, not only as it loads.
- A component's Overview is `Docs/Overviews/{Name}Overview.razor` when written by hand (see
  Button's); otherwise one is generated from the metadata in the same order: the doc comment's
  summary, its example as Usage, each of the component's examples in `components.json` under its
  title and `lead` (one or two sentences: what the feature does and the parameter that turns it
  on; backticks mark code), then `after`, a list of short paragraphs under the example for the
  details, caveats and mode differences (a site test keeps a lead or an `after` paragraph to 60
  words and 3 sentences, a topic intro's paragraph to 80 words and 4; on a hand-written page
  both are for search and MCP only, so they say what the page says), with an
  optional `info` (`title` and `text`) shown as an info Alert under the lead, only for a caveat readers
  would otherwise trip on, the remarks as Details, its parts, Mode support, Accessibility, Parameters and See also.
  The API page is always generated.
- A complex component isn't explained on a single page: it gets topic pages under it
  (`components.json` "topics", as DataGrid's, Table's and ListView's). A component needs them
  when it has several separate features, each with its own parameters and examples, such as
  selection, loading data and reordering. Length alone doesn't count: many variations of one idea
  (Card's layouts, Button's variants) stay on one page. The Overview keeps the basics a newcomer
  needs first. Use the same topic names across components where the feature is the same
  (Filtering, Selection, Loading data). Each topic lists its examples and the members its own API page shows. A
  topic's page is `Docs/Topics/{Name}{Topic}Topic.razor` when written by hand (`TableFilteringTopic`
  for Table's `filtering`), else its `intro` and its examples under their titles. A hand-written
  Overview lists them under a "Topics" heading with `<OverviewTopics />`.
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
  `Accessibility:` starts the accessibility bullets, `Keyboard:` the keys, and `See also: Select,
  Combobox.` names related components; these go at the end. Under `Keyboard:`, a line titles the
  keys for one place focus can be, and `- Up, Shift+Up | What they do` is a row; the docs build
  fails on a malformed one. A hand-written Overview shows them with
  `@KeyboardTable.Section(ApiComponent)`.
- A page's head is `<PageMeta Title="..." Description="..." />` (Parts), not `PageTitle` and
  `HeadContent`: it writes the description and Open Graph tags from the same two values, and the
  canonical link (izzyblazor.com, the path without the query, so `?mode=` and an example's keys
  aren't separate pages). Keep the description under 160 characters (a site test checks);
  `Indexed="false"` for a page search engines should skip. A new kind of page also goes in
  `Services/Sitemap.cs`, which serves `sitemap.xml` and `robots.txt`.
- `Api/released.api.json` is the last release's API (names only). The build compares with it to
  mark what each release added ("Added in") and print what it removed; it's refreshed when a
  release is cut (see Releasing).

#### Hosting

The site is at [izzyblazor.com](https://izzyblazor.com), on the Fly.io app `izzyblazor`
(`fly.toml`, `Dockerfile` at the root). The image builds the site with the .NET 10 SDK and Node 22
(the library's asset build) and runs it on the ASP.NET Core image; `docker build .` builds the same
image locally. It runs on one always-on machine: a Live example's circuit stays on the machine that
served its page, so a second machine drops them: Live examples stop answering while their controls
still move, and the console shows `No Connection with that ID: Status code '404'` (the
troubleshooting article `interactive-stops-responding`). Always deploy with `--ha=false`, which
keeps Fly from creating a second machine. `fly machine list -a izzyblazor` should show one; if
there are two, `fly scale count 1 -a izzyblazor`. On Oct 26, 2026 a second machine appeared
outside the Release workflow and broke the Live examples until it was removed.

- The Release workflow deploys it after the packages are pushed, so the docs match the release.
  `fly deploy --remote-only --ha=false` deploys main by hand, but the site builds the library from
  source, so that also puts unreleased library changes on the site.
- `www.izzyblazor.com`, `izzyblazor.dev` and `www.izzyblazor.dev` redirect to izzyblazor.com with a
  301 that keeps the path and query (`Services/CanonicalHost.cs`). It names those hosts rather than
  redirecting every other one, since Fly's health checks and `izzyblazor.fly.dev` arrive with other
  hosts.
- Crawlers: the examples keep their state in the query (Pagination's `?page=&small=...`), so their
  links make endless addresses. On Oct 27, 2026 GPTBot walked them at about two requests a second,
  which throttled the shared CPU and slowed every page to 2-6s (pages render in ~30ms). robots.txt
  disallows `/*?`, and pages that come through Fly's proxy (with `Fly-Client-IP`) are limited to 60
  a minute per client (`SiteApp`, the `pages` policy; not `/frame/`, the circuit, or re-executed
  error pages). The first rejection a minute is logged as `Rate limited <client> (<user agent>)`.
  If the site is slow, compare a page's time with a local Release run and read
  `fly logs -a izzyblazor`: request lines end with their duration.
- Fly ends TLS and forwards plain HTTP with `X-Forwarded-Proto`, which `UseForwardedHeaders` in
  `SiteApp` reads, so absolute addresses (`og:image`) stay `https`.
- Fly holds a certificate for each of the four names and renews it. DNS for both domains is at
  Hostinger: `A` and `AAAA` records on `@` with the app's addresses (`fly ips list -a izzyblazor`),
  and `www` a CNAME to the apex. A new name needs `fly certs add <name> -a izzyblazor`, its DNS
  records, and, if it should redirect, an entry in `CanonicalHost`.

#### Writing an Overview

An Overview is for using the component; the API page is the reference. Follow Button's:

1. **Usage**: a short code block of the common cases, ready to copy.
2. **A section per feature**, in this order: what it is, as the user sees it; when to use it,
   next to the alternatives; how to turn it on; then the details. "A filter row is a row of
   fields under the column titles. The user types in a field to filter that column without
   opening a panel, which suits grids that people filter all day. Set `FilterRow` on the grid to
   add it." Never open with the parameter ("Use `FilterRow` for…"): the reader doesn't know yet
   what it is. Then its example. Details such as ARIA attributes and edge cases go in the
   parameter's doc comment or the Accessibility section. A `components.json` lead follows the
   same order.

   Order the sections by what the reader needs first. Open with what the component is for: its
   core behavior (opening a dialog, sorting and paging a table), or its look when that's what
   it's chosen by (Button's variants, Badge, Card). Then its structure and content, its look kept
   together (variants beside sizes), its states, then state across requests and other settings
   (binding, the URL, several on a page, in a form, from code). **Labels and localization** is
   always the last feature section.
3. **Mode support**, **Accessibility** (from the doc comment, as above), **Parameters**
   (`<ParameterTable Compact />`) and **See also**, after the examples.

A component with topics lists them after Usage. Make a feature a topic, not an Overview section,
when it needs real explanation or three or more examples that build on each other, has setup of
its own (a data provider, a service, a model), works differently enough in Static and Interactive
that a note under the example won't do, or would push what most users need down the page. Keep it
on the Overview when most users need it and a sentence or two and one example cover it. Order the
topics by how often they're needed (DataGrid's Editing comes right after Columns), and put what
every user needs before shipping early (RichTextEditor's Security, after Toolbar). A topic opens
with its `summary` as the lead, then any setup, the basic case, and variations after what they
build on. Mode support, Accessibility and Parameters stay on the Overview.

For each section, pick how to show it by what the reader needs:

| Show | When | Such as |
| --- | --- | --- |
| A preview (`<ExampleViewer />`), source a click away | It looks or behaves differently, or Off · Static · Live changes the result | Sizes, variants, states, interactions, OnClick |
| Code only (`<Code Block>`) | There's something to copy, and a preview would show nothing new | Usage, setup, a pattern to paste, behavior seen only after a post |
| Prose only | Nothing renders | Accessibility, gotchas |

The test: would the preview show something the code doesn't? If not, show the code. Give an
example its own `notes` in `components.json` only for a mode that changes what it does; without
them the viewer shows none.

#### Writing style

This applies to every page, article, topic, `components.json` lead and note, and doc comment. The
reader is a .NET developer who knows Blazor and may come from MudBlazor or Radzen.

- **Write whole sentences.** Each clause has its own verb. Not "Text matches anywhere, a date its
  day"; write "Text matches anywhere in the value. A date matches the whole day."
- **One fact per sentence.** A section opens with one or two sentences: what the feature does,
  and the parameter that turns it on. Caveats, limits and mode differences come after the example, as short sentences or a
  list. Keep a paragraph to about three sentences. Where there are rows (a type and its operator, a
  parameter and its default), use a table.
- **Use plain verbs.** "requires", not "takes"; "sets", not "says"; "the server rejects", not
  "turns away"; "checked", not "ticked". Avoid "holds", "draws" and "keeps it right" as
  technical terms.
- **Say what the user needs, not how it was built.** Leave out browser versions, the arithmetic
  behind a size, algorithm costs and why a limit exists, unless the reason changes what someone
  does.
- **Options are goal first**: "To list every active filter below the table, set `FilterPanel`."
  Not "Use `FilterPanel` to…".
- **Say what an example shows when it isn't obvious**, in its own terms: "This example turns off
  sorting on the Order column." Don't lean on a label inside it as if the reader had seen it ("as
  Discount (%) does"); state the rule.
- **Explain a shared caveat once**, on the article it belongs to (Forms, Render modes), and link
  to it in one line.
- **Headings say what the section is for**: "Labels and localization", not "Its own words";
  "Indeterminate state", not "A dash of your own". Example titles follow the same rule.
- **A component's summary** starts with what it does, as a verb: "Displays rows of data in a grid
  users can sort, filter, group and edit." Where a simpler or related component fits some readers
  better, the Overview's first paragraph says so: "For a plain list of records, see Table."
- **Be direct, in the active voice.** Name the feature and say what it does, or what the user
  does with it: "Column filters narrow the rows from each column's header. The filter is kept in
  the URL and can be set from C#." No build-up ("Let users…", "This page shows…"), and never a
  list of the page's sections ("Filters in column headers, lists of values, operators, and
  filters in the URL"). Topic summaries and intros follow this; so does any lead. If a sentence
  reads as a list of nouns, rewrite it.

Use the same word for the same idea everywhere:

| Say | Not |
| --- | --- |
| with JavaScript off | with no script, without the script, before the script |
| IzzyBlazor's script (linked to Render modes the first time on a page) | the script, with script |
| on static pages, on interactive pages | on a static page, on an interactive page |
| without a full reload | without a reload, without a page load, with no page load |
| URL | address, query string |
| the user (someone using the app); you (the developer) | the reader, people, someone |
| render mode (Static, Interactive); paging style | mode, for anything other than a render mode |
| the browser's default message; your `[Required]` message | in its own words, in the model's words |
| a form post | a static post, a post |
| the page reloads (when it does) | the page loads again, the page comes back, loads the page again |
| contains, stores; accepts, has; sets | holds; takes; says |

Fixed wordings, so pages read alike:

- An Overview opens with the concept in lower case, then when it suits: "A switch is a checkbox
  that reads as on or off. It suits settings." Not "A Switch is…" or "Use it for…".
- Read-only: "For a value the user can see but not change, set `ReadOnly`; it still posts."
- The Forms caveats, linked in one form: "Before saving a disabled value from a form post, see
  [Disabled fields and form posts]." and "Before trusting a posted value, see [Values a form post
  can change]."
- The first use on a page of static, interactive, JavaScript off or IzzyBlazor's script links to
  Render modes.
- A component whose labels can be localized has a "Labels and localization" section.
- Troubleshooting titles name the symptom as the user meets it ("OnClick does nothing on a static
  page") and may stay singular.

Define a term before using it. The terms used most (static, interactive, circuit, JavaScript off)
are defined on Render modes; link there instead of redefining them.

### Tests

**A test has to earn its place.** Before adding one, it must pass all of these:

1. It covers something a person or an app really does, not an edge condition or a race.
2. If it broke, someone would care, and nothing else would catch it. Restating the markup doesn't count.
3. It doesn't rely on delays, slowed responses or load, and gives the same result every run.
4. It's the only test of that behaviour, with no copy per mode unless the modes run different code.

Fixing a bug doesn't earn a test by itself. A flaky test is deleted or simplified, not engineered around.

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

- `...Field`: renders its own label, hint and errors in the field box (`TextField`, `NumberField`,
  `DateField`, `DateRangeField`, `TimeField`; later `SearchField`). Most form controls are fields.
- `...Input`: a bare control with no label, for custom layouts. None today.
- `...Picker`: opens a panel to choose from, with no input to type in (`ColorPicker`). A field that
  takes typing and also opens a panel stays a `...Field` (`DateField`).

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
- tests cover its behavior (not its markup; see [Tests](#tests)), and the docs site
  covers it (its Overview, see [The docs site](#the-docs-site)).

## Commits

One logical change per commit, with messages in the form `Area: what changed`, for example
`Combobox: keep focus in the input after a click`.

## Releasing

Pushing a version tag publishes both packages and deploys the docs site. Set `<Version>` in
`Directory.Build.props` and the version in the preview notes of `README.md` and
`docs/nuget-readme.md`, commit it to main, then tag that commit with the same version:

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
creator). Its last job deploys the docs site (see Hosting) with the `release` environment's
`FLY_API_TOKEN` secret, a deploy token for the `izzyblazor` app (`fly tokens create deploy -a
izzyblazor`); Fly's builder builds the image, so the job only holds the token. A failed run can be
rerun: packages already on nuget.org are skipped. A published version can't be replaced, only
unlisted, so a fix needs a new version.
