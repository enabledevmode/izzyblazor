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

## Themes

The default theme is **Blue on neutral grays**, with white cards, tables and fields on a near-white page. Pick a named theme, or your own
accent, gray and corner radius, with attributes on `<html>` in `App.razor`:

```html
<html lang="en" data-ib-theme="ember">                          <!-- a named theme -->
<html lang="en" data-ib-accent="blue" data-ib-gray="stone">     <!-- your own combination -->
<html lang="en" data-ib-theme="classic" data-ib-radius="lg">    <!-- a named theme, adjusted -->
```

Named themes are presets of the sets below; an explicit `data-ib-accent`, `data-ib-gray` or
`data-ib-radius` beats the theme's.

| `data-ib-theme` | Accent | Gray | Radius | Headings |
| --- | --- | --- | --- | --- |
| `ember` | Ember | Neutral | md | Bricolage Grotesque, bold |
| `classic` | Blue | Slate | md | |
| `editorial` | Ink | Stone | lg | Newsreader (serif) |
| `harbor` | Teal | Slate | md | |
| `grove` | Green | Neutral | md | |
| `studio` | Violet | Neutral | lg | |
| `crisp` | Indigo | Neutral | sm | |

| Attribute | Values |
| --- | --- |
| `data-ib-accent` | `blue` (default), `ink` (monochrome), `indigo`, `violet`, `teal`, `green`, `rose`, `ember` |
| `data-ib-gray` | `neutral` (default), `stone` (warm paper; pairs with serif headings), `slate` (cool) |
| `data-ib-radius` | `sm`, `md` (default), `lg` |

Every accent is tuned by hand in light and dark: text holds 4.5:1 on its fill and on every gray. The
attributes work on any element too, for part of a page; set accent and gray on the same element.
They're plain CSS, so they work in every render mode with no script.

**Fonts.** Body text uses the system font (`--ib-font-sans`). Headings, card and dialog titles use
`--ib-font-heading` at `--ib-weight-heading`: by default the body font, semibold. The `ember` theme
uses Bricolage Grotesque and `editorial` uses Newsreader; load the font in your `<head>` (or
self-host it; both are under the SIL Open Font License):

```html
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:opsz,wght@12..96,500..700&family=Newsreader:opsz,wght@6..72,500..600&display=swap" />
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
