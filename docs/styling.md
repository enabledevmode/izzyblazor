# Styling

IzzyBlazor's styling is typed, responsive and resolved on the server. There is no CSS-in-JS and
nothing computed in the browser: each value becomes a class and a CSS custom property.

## Typed values

Closed sets are enums with IntelliSense, so mistakes fail at compile time:

- **Layout:** `Direction.Row`, `Align.Center`, `Justify.Between`
- **Shape:** `Radius.Md`, `Shadow.Sm`
- **Colors:** `Color` tokens such as `Color.SurfaceContainer`
- **Sizes:** `Length` values such as `Length.Prose`, `Length.Full`, `Length.Rem(15)`

## Spacing

Spacing is an `int` step from the scale: `Padding="4"`, from 0 to 16. Steps 0 to 12 go up 4px at a
time (0 to 48px); 13 to 16 are 56, 64, 80 and 96px. The steps are the `--ib-space-*` tokens. Margins
go from -16 to 16, where a negative step pulls the element outward.

- `PaddingX` and `PaddingY` override one axis.
- `PaddingTop`, `PaddingBottom`, `PaddingStart` and `PaddingEnd` override one side. Start and End
  follow the text direction, so layouts flip correctly in right-to-left languages.
- `Margin` works the same way (`MarginX`, `MarginTop`, and so on).

A side beats its axis, which beats the all-sides value, at every breakpoint.

`Box` and the components built on it (Stack, Row, Grid, Section, Card, the typography components and
the like) take these props. The App layout parts take `Bg` and `ColorScheme` only, and controls such as
fields and buttons take none: space them with the layout around them (`Stack Gap`, `Row Gap`).

## Responsive values

Every style prop takes a single value or a breakpoint map:

```razor
<Stack Gap="4">
<Stack Gap="new() { Base = 2, Md = 6 }">
<Stack Direction="new() { Base = Direction.Column, Md = Direction.Row }">
```

Breakpoints: `Sm` 640px, `Md` 768px, `Lg` 1024px, `Xl` 1280px. Only the values you set produce CSS,
so `new() { Md = 6 }` keeps the component's default below 768px.

`HiddenFrom="Breakpoint.Md"` and `VisibleFrom="Breakpoint.Md"` work on any styled component.

What's responsive and what isn't follows one rule: things that change with screen size (direction,
gap, spacing, columns, sizes, alignment, visibility) take a breakpoint map; on/off switches (`Grow`,
`Shrink`, `Wrap`, `Inline`, `Stretch` and the rest) are plain booleans, so they can be written bare:
`<Box Grow>`, `<Row Wrap>`. Razor only allows the bare form for plain booleans.

Text can be fluid instead of stepped: `MinSize` on `TextBlock` and `Heading` makes the text
`MinSize` on a 360px screen, growing smoothly to `Size` at 1280px
(`<Heading Size="TextSize.Xl5" MinSize="TextSize.Xl3">`). See
[Typography](components.md#typography-links-and-code).

## Text color

Text uses `Color.OnSurface` by default.

- **Emphasis levels:** `Color.Muted`, `Color.Subtle`, `Color.Disabled`.
- **Accent text** that stays readable on surfaces (at least 4.5:1 in both themes):
  `Color.PrimaryText`, `SuccessText`, `WarningText`, `DangerText`, `InfoText`.
- **Automatic contrast:** setting `Bg` to a theme color picks the matching text color
  (`Bg="Color.Primary"` gets `OnPrimary`). An explicit `Color` always wins. Custom tokens pair with
  `on-{name}` when your theme defines it.

## Actions and status colors

Actions get three colors: `Accent.Primary`, `Neutral` and `Danger`. Emphasis comes from the variant
(`Solid`, `Soft`, `Outline`, `Ghost`, `Link`), not from more hues.

Feedback (badges, alerts and toasts) uses `Status`: `Neutral`, `Info`, `Success`, `Warning`, `Danger`.

For anything else, pass a `Color`: `<Button Accent='Accent.Of("#0f766e")'>` or
`Color='Color.Token("brand")'`. Text on it is picked for contrast: the token's `on-` partner, or near-black
or white for a hex color.

## Presets and colors

The default is the **Standard** preset in **Blue on neutral grays**: white cards, tables and fields on
a near-white page. Declare and customize the theme in one place, `IzzyTheme` in `App.razor`'s `<head>`: a preset for
density, shape and type, then an accent, gray and radius over the preset's defaults, or your own
brand color.

```razor
@* App.razor, in <head>, for example: *@
<IzzyTheme Preset="ThemePreset.Compact" />                           @* a preset *@
<IzzyTheme Preset="ThemePreset.Compact" Accent="ThemeAccent.Teal" /> @* a preset in another accent *@
<IzzyTheme Preset="ThemePreset.Soft" Primary="#0a7d5a" />            @* a preset in your brand color *@
```

| `Preset` (`data-ib-preset`) | For | Controls | Shape | Type | Accent | Gray |
| --- | --- | --- | --- | --- | --- | --- |
| `standard` (default) | Internal tools, line-of-business | 36px | Medium radius, hairline shadow | System font | Blue | Neutral |
| `compact` | Admin, ERP, finance, data grids | 32px, tight rows | Small radius, borders only | System font | Indigo | Slate |
| `soft` | Consumer apps, education, health, portals | 40px, roomy | Large radius, gentle shadow | System font | Teal | Neutral |
| `editorial` | Docs, publishing, knowledge bases | 36px | Large radius | IBM Plex Sans, Newsreader headings | Ink | Stone |

A preset's accent, gray and radius are only its defaults: `Accent`, `Gray` and `Radius` beat them,
and `Primary` beats `Accent`.

`IzzyTheme` renders meta tags the theme CSS matches (and a small `<style>` block for `Primary`, the
fonts and `HeadingWeight`), so
it's in place on first paint, needs no script and works in every render mode. The same choices
work as attributes, on `<html>` or on any element to restyle part of a page:

```html
<html lang="en" data-ib-preset="compact" data-ib-accent="teal">
<section data-ib-accent="rose">...</section>
```

| `IzzyTheme` / attribute | Values |
| --- | --- |
| `Accent` / `data-ib-accent` | `blue` (default), `ink` (monochrome), `indigo`, `violet`, `teal`, `green`, `rose` |
| `Gray` / `data-ib-gray` | `neutral` (default), `stone` (warm paper; pairs with serif headings), `slate` (cool) |
| `Radius` / `data-ib-radius` | `sm`, `md` (default), `lg` |
| `Density` / `data-ib-density` | `compact` (32px), `standard` (36px, default), `comfortable` (40px) |

**Your brand color.** `Primary` makes it the accent:

```razor
<IzzyTheme Primary="#0a7d5a" />
```

From that one color IzzyBlazor derives the fill, the text on it, the accent as text and its soft
tint, in light and dark (`AccentPalette.From`), and checks each pair: text on the fill holds 4.5:1
(white or near-black, whichever reads better), the accent as text holds 4.5:1 on the surface, the
gray fills and its tint, and in dark mode a dark brand color is lifted until it shows. The fill is
your color as given unless no text can reach 4.5:1 on it; then only its lightness moves. Focus
rings, the field halo, selected rows, switches, sliders and radios follow. The server renders it as
a small `<style>` block, so it's there on first paint with no script; pass `Nonce` for a strict
Content-Security-Policy. It beats a preset's accent; `data-ib-accent` on an element inside the page
still recolors that part.

To tune the shades yourself, set the four tokens instead:

```css
:root {
  --ib-color-primary:      light-dark(#0a7d5a, #0a7d5a);  /* the fill */
  --ib-color-on-primary:   light-dark(#ffffff, #ffffff);  /* text on the fill */
  --ib-color-primary-text: light-dark(#00714e, #6ccba4);  /* the accent as text */
  --ib-color-primary-soft: light-dark(#dceae3, #202f2b);  /* the soft tint */
}
```

Every accent is tuned by hand in light and dark: text holds 4.5:1 on its fill and on every gray. The
attributes work on any element too, for part of a page; set accent and gray on the same element.
They're plain CSS, so they work in every render mode with no script.

**Fonts.** Body text uses the system font (`--ib-font-body`). Headings, card and dialog titles use
`--ib-font-heading` at `--ib-weight-heading`: by default the body font, semibold. The `editorial`
preset uses IBM Plex Sans and Newsreader; load them in your `<head>` (or self-host them; both are
under the SIL Open Font License):

```html
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600&family=Newsreader:opsz,wght@6..72,500..600&display=swap" />
```

Code, `Kbd` and the color picker's hex box use `--ib-font-mono`, the system's monospace font. Set
your own with `IzzyTheme`'s `BodyFont`, `HeadingFont`, `MonoFont` and `HeadingWeight` (below).

**The page and its surfaces.** The page is `--ib-color-background` (`Color.Background`); cards,
tables, fields and bars are `--ib-color-surface`. Menus, popovers, dialogs and toasts float on
`--ib-color-surface-raised`, a step lighter than cards in dark mode. For a white page, set
`--ib-color-background` to the surface color.

Raised things are lighter than what they sit on; recessed things are darker:

| Layer | Token | Use |
|---|---|---|
| Page | `background` | The page behind everything |
| Raised | `surface` (with a `border`) | Cards, panels, tables, bars: anything that sits on the page |
| Floating | `surface-raised` | Menus, popovers, dialogs, toasts |
| Recessed | `surface-container-low` | Large quiet fills inside a surface: a filled card, a panel |
| | `surface-container` | Small fills that must show: table heads, code, wells, tracks |
| | `surface-container-high` | Selected and pressed fills, chips |

A container fill placed directly on the page reads as a hole, not a card; put a surface between
them. (In dark mode the page is the darkest layer and fills step lighter, so the same rule holds.)

`AppShell`'s `Variant` decides which parts of the frame take the page tint: `Tinted` (the default,
the sidebar), `Inset`, `Flat` or `Canvas`. See [App layout](components.md#app-layout).

## Customizing

Two levels. `IzzyTheme` takes the choices that move many values together and keeps them right
(contrast, light and dark pairs, whole pixels). CSS variables take single, exact values.

```razor
<IzzyTheme Preset="ThemePreset.Soft" Density="ThemeDensity.Compact" Primary="#0a7d5a"
           BodyFont="Inter, system-ui, sans-serif" HeadingWeight="700" ColorScheme="ColorScheme.Light" />
```

| `IzzyTheme` | Sets | Values |
| --- | --- | --- |
| `Preset` | Density, shape, type and default colors together | `Standard`, `Compact`, `Soft`, `Editorial` |
| `Primary` | Your brand color: fill, text on it, accent text and soft tint, light and dark, each at AA | Hex |
| `Accent` | A built-in accent instead | `Blue`, `Ink`, `Indigo`, `Violet`, `Teal`, `Green`, `Rose` |
| `Gray` | Page, surfaces, fills, borders and text | `Neutral`, `Slate`, `Stone` |
| `Radius` | Controls, cards and dialogs, in proportion | `Sm`, `Md`, `Lg` |
| `Density` | Control heights, table rows and card padding | `Compact` (32px), `Standard` (36px), `Comfortable` (40px) |
| `BodyFont`, `HeadingFont`, `MonoFont` | Body, heading and code font families (load the fonts yourself) | CSS font-family lists |
| `HeadingWeight` | Headings' weight | 100 to 900 |
| `Scheme` | Light or dark until the user picks one with a `ColorSchemeSwitch` | `System`, `Light`, `Dark` |

Each of `Accent`, `Gray`, `Radius`, `Density` and the fonts beats the preset's own; leave them unset to
take the preset's. The same choices work as `data-ib-preset`, `data-ib-accent`, `data-ib-gray`,
`data-ib-radius` and `data-ib-density` attributes, on `<html>` or on part of a page.

For one exact value, set its variable in your own stylesheet. IzzyBlazor's CSS sits in cascade
layers, so a plain rule wins:

```css
:root {
  --ib-color-focus: #7c3aed;     /* the focus ring and field halo */
  --ib-card-padding: 1.5rem;
  --ib-radius-xl: 0.5rem;        /* cards and other large panels, not the whole scale */
}
```

| Group | Variables |
| --- | --- |
| Colors | `--ib-color-background`, `-surface`, `-surface-raised`, `-surface-container-low`, `-surface-container`, `-surface-container-high`, `-border`, `-outline`, `-on-surface`, `-muted`, `-subtle`, `-disabled`, `-focus`; the accent's four (`-primary`, `-on-primary`, `-primary-text`, `-primary-soft`); status (`-success`, `-warning`, `-danger`, `-info`, each with `-text` and `-soft`) |
| Shape | `--ib-radius-sm`, `-md`, `-lg`, `-xl` |
| Density | `--ib-control-h-sm`, `-md`, `-lg` (buttons, fields, tabs, pagination, nav items, menu rows), `--ib-control-font-sm` (text in small controls, 13px), `--ib-table-cell-py`, `--ib-card-padding`, the spacing scale `--ib-space-*` |
| Type | `--ib-font-body`, `--ib-font-heading`, `--ib-font-mono`, `--ib-weight-heading`, the size scale `--ib-text-*` |
| Depth | `--ib-shadow-xs` (under cards), `-sm`, `-md`, `-lg` |
| Motion | `--ib-duration-fast`, `--ib-duration-normal`, `--ib-ease` |
| Code | `--ib-code-comment`, `-keyword`, `-string`, `-number`, `-type`, `-function`, `-tag`, `-attr`, `-meta` (syntax colors, each at least 4.5:1 on the code block) |
| Scrollbars | `--ib-color-scrollbar`, `--ib-color-scrollbar-hover` (the thumb; see [Scrollbars](components.md#scrollbars)) |

Colors are `light-dark(light, dark)` pairs, so one declaration covers both schemes; keep text at
4.5:1 on what it sits on. Keep sizes whole pixels. Status colors are tuned for contrast against
every gray, so change them as a set.

## Tokens

Everything the components draw (colors, spacing, radius, fonts, shadows, motion) comes from CSS
custom properties prefixed `--ib-`. A theme is a stylesheet that sets new values. The tokens live in
a cascade layer (`izzy.tokens`), so your own CSS wins without `!important` or extra specificity.

Most tokens have a single value. Override them on `:root`:

```css
:root {
  --ib-font-body: "Inter", system-ui, sans-serif;
  --ib-radius-md: 0.5rem;
}
```

Colors hold both schemes in one value with CSS `light-dark(light, dark)`, and each element's
`color-scheme` picks the half. So one override covers light mode, dark mode, `data-ib-color-scheme`, and parts
of the page with their own `ColorScheme`:

```css
:root {
  --ib-color-primary: light-dark(#0f766e, #2dd4bf);
  --ib-color-on-primary: light-dark(#ffffff, #042f2e);
  --ib-color-primary-text: light-dark(#115e59, #5eead4);   /* readable text on surfaces */
  --ib-color-primary-soft: light-dark(#d9e9e8, #12302e);   /* soft buttons, current page, chips */
}
```

Give a color the same value twice, or a plain color, if it shouldn't change with the scheme. Each
accent has an `on-` partner (text on it) and most have a `-text` variant tuned for text on surfaces;
change them together so contrast holds.

Your own colors work the same way: define `--ib-color-brand` (and `--ib-color-on-brand`) with
`light-dark()`, then use `Color.Token("brand")`.

Dark mode follows the system unless the page chooses: `IzzyTheme`'s `ColorScheme` sets the app's default
(`ColorScheme.Light`, `Dark` or `System`), a [`ColorSchemeSwitch`](components.md#colorschemeswitch)
lets the user choose (kept in the `ib-scheme` cookie, which `IzzyTheme` reads on the server with
`AddIzzyBlazor()`, so pages arrive in the chosen scheme with no flash), and `<html data-ib-color-scheme="light|dark">` still works. Browsers without `light-dark()` (before
2024) get the light colors everywhere.

## A color scheme for part of a page

`ColorScheme` on `Box` (and everything built on it, such as `Stack` and `Card`) and on the App layout
parts gives that part its own scheme:

- `ColorScheme.Dark` for a dark sidebar or header in a light app,
- `ColorScheme.Light`,
- `ColorScheme.Inverse` for the opposite of the page, worked out in CSS, including when the page follows
  the system.

Every token switches inside it, so text, links, buttons, badges, fields, focus rings and menus opened
from it keep their contrast. `Bg` still applies on top (`Bg="Color.SurfaceContainer"` is the scheme's
container shade). Outside components, `data-ib-color-scheme="dark|light|inverse"` does the same on
any element.

## Escape hatches

- `Color.Token("brand-500")` for your own theme tokens, and `Color.Of("#ff6600")` for a literal color.
- `Length.Of("calc(100% - 2rem)")` for any CSS length.

Raw values are checked for characters that could break out of a style.

## Unit shorthands (optional)

Add `@using IzzyBlazor.Units` to write `Width="15.Rem"`, `MinHeight="100.Dvh"` or `Basis="240.Px"`
(C# 14 extension properties). They live in their own namespace because they add members to every
`int` and `double` in files that import them.

## Under the hood

Each value becomes a modifier class plus a custom property:

```html
<div class="ib-box ib-px ib-px-md" style="--ib-px:var(--ib-space-4);--ib-px-md:var(--ib-space-10)">
```

All CSS sits in cascade layers (`izzy.tokens`, `izzy.base`, `izzy.components`, `izzy.utilities`), so
your own CSS always wins without needing more specific selectors. Style props are in
`izzy.utilities`, so they beat a component's own styles.
