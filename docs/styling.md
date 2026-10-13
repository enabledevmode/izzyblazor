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

Spacing is an `int` step from the scale: `Padding="4"`, from 0 to 16, where one step is 4px. Margins
go from -16 to 16.

- `PaddingX` and `PaddingY` override one axis.
- `PaddingTop`, `PaddingBottom`, `PaddingStart` and `PaddingEnd` override one side. Start and End
  follow the text direction, so layouts flip correctly in right-to-left languages.
- `Margin` works the same way (`MarginX`, `MarginTop`, and so on).

A side beats its axis, which beats the all-sides value, at every breakpoint.

Layout and surface components (Box, Stack, Grid, Card, the App layout parts and the typography
components) take these props. Controls such as fields and buttons don't: space them with the layout
around them (`Stack Gap`, `Row Gap`).

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

For anything else, pass a `Color`: `<Button Color='Color.Of("#0f766e")'>` or
`Color='Color.Token("brand")'`. Text on it is picked for contrast: the token's `on-` partner, or black
or white for a hex color.

## Looks and colors

The default is the **Standard** look in **Blue on neutral grays**: white cards, tables and fields on
a near-white page. Declare and customize the theme in one place, `IzzyTheme` in `App.razor`'s `<head>`: a look for
density, shape and type, then an accent, gray and radius over the look's defaults, or your own
brand color.

```razor
@* App.razor, in <head>, for example: *@
<IzzyTheme Look="ThemeLook.Compact" />                           @* a look *@
<IzzyTheme Look="ThemeLook.Compact" Accent="ThemeAccent.Teal" /> @* a look in another accent *@
<IzzyTheme Look="ThemeLook.Soft" Primary="#0a7d5a" />            @* a look in your brand color *@
```

| `Look` (`data-ib-theme`) | For | Controls | Shape | Type | Accent | Gray |
| --- | --- | --- | --- | --- | --- | --- |
| `standard` (default) | Internal tools, line-of-business | 36px | Medium radius, hairline shadow | System font | Blue | Neutral |
| `compact` | Admin, ERP, finance, data grids | 32px, tight rows | Small radius, borders only | System font | Indigo | Slate |
| `soft` | Consumer apps, education, health, portals | 40px, roomy | Large radius, gentle shadow | System font | Teal | Neutral |
| `editorial` | Docs, publishing, knowledge bases | 36px | Large radius | IBM Plex Sans, Newsreader headings | Ink | Stone |

A look's accent, gray and radius are only its defaults: `Accent`, `Gray` and `Radius` beat them,
and `Primary` beats `Accent`.

`IzzyTheme` renders meta tags the theme CSS matches (and a small `<style>` block for `Primary`), so
it's in place on first paint, needs no script and works in every render mode. The same choices
work as attributes, on `<html>` or on any element to restyle part of a page:

```html
<html lang="en" data-ib-theme="compact" data-ib-accent="teal">
<section data-ib-accent="rose">...</section>
```

| `IzzyTheme` / attribute | Values |
| --- | --- |
| `Accent` / `data-ib-accent` | `blue` (default), `ink` (monochrome), `indigo`, `violet`, `teal`, `green`, `rose` |
| `Gray` / `data-ib-gray` | `neutral` (default), `stone` (warm paper; pairs with serif headings), `slate` (cool) |
| `Radius` / `data-ib-radius` | `sm`, `md` (default), `lg` |

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
Content-Security-Policy. It beats a look's accent; `data-ib-accent` on an element inside the page
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

Density is a handful of tokens, so you can set your own: `--ib-control-h-sm`, `--ib-control-h-md`,
`--ib-control-h-lg` (buttons, fields, tabs, pagination, nav items and menu rows),
`--ib-table-cell-py` and `--ib-card-padding`. Keep them whole pixels.

Every accent is tuned by hand in light and dark: text holds 4.5:1 on its fill and on every gray. The
attributes work on any element too, for part of a page; set accent and gray on the same element.
They're plain CSS, so they work in every render mode with no script.

**Fonts.** Body text uses the system font (`--ib-font-sans`). Headings, card and dialog titles use
`--ib-font-heading` at `--ib-weight-heading`: by default the body font, semibold. The `editorial`
look uses IBM Plex Sans and Newsreader; load them in your `<head>` (or self-host them; both are
under the SIL Open Font License):

```html
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600&family=Newsreader:opsz,wght@6..72,500..600&display=swap" />
```

Or set your own: `:root { --ib-font-sans: "IBM Plex Sans", system-ui, sans-serif; --ib-font-heading: Newsreader, Georgia, serif; --ib-weight-heading: 500; }`.

**The page and its surfaces.** The page is `--ib-color-background` (`Color.Background`); cards,
tables, fields, bars and menus are `--ib-color-surface`. For a white page, set
`--ib-color-background` to the surface color.

Raised things are lighter than what they sit on; recessed things are darker:

| Layer | Token | Use |
|---|---|---|
| Page | `background` | The page behind everything |
| Raised | `surface` (with a `border`) | Cards, panels, tables, bars, menus: anything that sits on the page |
| Recessed | `surface-container-low` | Large quiet fills inside a surface: a filled card, a panel |
| | `surface-container` | Small fills that must show: table heads, code, wells, tracks |
| | `surface-container-high` | Selected and pressed fills, chips |

A container fill placed directly on the page reads as a hole, not a card; put a surface between
them. (In dark mode the page is the darkest layer and fills step lighter, so the same rule holds.)

## Tokens

Everything the components draw (colors, spacing, radius, fonts, shadows, motion) comes from CSS
custom properties prefixed `--ib-`. A theme is a stylesheet that sets new values. The tokens live in
a cascade layer (`izzy.tokens`), so your own CSS wins without `!important` or extra specificity.

Most tokens have a single value. Override them on `:root`:

```css
:root {
  --ib-font-sans: "Inter", system-ui, sans-serif;
  --ib-radius-md: 0.5rem;
}
```

Colors hold both schemes in one value with CSS `light-dark(light, dark)`, and each element's
`color-scheme` picks the half. So one override covers light mode, dark mode, `data-theme`, and parts
of the page with their own `Scheme`:

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

Dark mode follows the system unless `<html data-theme="light|dark">` is set. Browsers without
`light-dark()` (before 2024) get the light colors everywhere.

## A color scheme for part of a page

`Scheme` on `Box` and on the App layout parts gives that part its own scheme:

- `Scheme.Dark` for a dark sidebar or header in a light app,
- `Scheme.Light`,
- `Scheme.Inverse` for the opposite of the page, worked out in CSS, including when the page follows
  the system.

Every token switches inside it, so text, links, buttons, badges, fields, focus rings and menus opened
from it keep their contrast. `Bg` still applies on top (`Bg="Color.SurfaceContainer"` is the scheme's
container shade). Outside components, `data-theme="dark|light"` or the `ib-scheme-*` classes do the
same on any element.

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
your own CSS always wins without needing more specific selectors.
