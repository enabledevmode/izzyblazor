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

## Borders, sizes and overflow

`Border` draws a solid border on every side, in whole pixels (0 to 16), and works like padding:
`BorderX` and `BorderY` override an axis, `BorderTop`, `BorderBottom`, `BorderStart` and `BorderEnd`
one side. `BorderColor` sets the color (`Color.Border` by default); on its own it recolors a
component's existing border. Sides you don't set keep the component's own.

```razor
<Section Bg="Color.SurfaceContainerLow" BorderY="1">...</Section>   @* a band with lines above and below *@
<Box Border="1" BorderColor="Color.Outline" Radius="Radius.Lg">...</Box>
```

`Width`, `MinWidth`, `MaxWidth`, `Height`, `MinHeight` and `MaxHeight` take a `Length`. `Overflow`
(`Visible`, `Hidden`, `Auto`, `Scroll`, `Clip`) says what happens to content that doesn't fit:
`MaxHeight="Length.Rem(20)" Overflow="Overflow.Auto"` scrolls it.

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

## Colors

`Color` is always the text (or stroke) color and `Bg` the background, as in CSS. A control's own
color is its `Accent` (below). Text uses `Color.OnSurface` by default.

- **Emphasis levels:** `Color.Muted`, `Color.Subtle`, `Color.Disabled`.
- **Soft fills** for a status: `Color.PrimarySoft`, `SuccessSoft`, `WarningSoft`, `DangerSoft`,
  `InfoSoft`. As a `Bg` each picks its matching text color (`DangerSoft` gets `DangerText`).
- **Focus:** `Color.Focus`, the focus ring and field halo color (the primary color unless the theme
  sets `--ib-color-focus`).
- **Accent text** that stays readable on surfaces (at least 4.5:1 in both themes):
  `Color.PrimaryText`, `SuccessText`, `WarningText`, `DangerText`, `InfoText`.
- **Automatic contrast:** setting `Bg` to a theme color picks the matching text color
  (`Bg="Color.Primary"` gets `OnPrimary`). An explicit `Color` always wins. Custom tokens pair with
  `on-{name}` when your theme defines it.

## Actions and status colors

An `Accent` is a color set, not one color: the fill, the text on it, a soft tint and the color as
text, which a component uses together. `Bg` is only a background. Controls (and Avatar) take an
`Accent`: the roles `Accent.Primary` (your brand color), `Neutral` and `Danger`.
Emphasis comes from the variant (`Solid`, `Soft`, `Outline`, `Ghost`, `Link`), not from more hues: a
solid control is filled with its accent; outline, ghost and link controls use it for text and border.

Feedback (badges, tags, alerts, banners and toasts) uses `Status`: `Neutral`, `Info`, `Success`,
`Warning`, `Danger`. It sets the icon and what screen readers hear as well as the color; `Accent` on
a badge, tag or banner replaces only the color.

For one control in another color, `Accent` takes any CSS color or a token:

```razor
<Button Accent='Accent.Of("#0f766e")'>Approve</Button>
<Badge Accent='Accent.Token("brand")'>New</Badge>
<Progress Value="80" Accent="Accent.From(Color.Success)" />
```

Text on it is picked for contrast: the token's `on-` partner, or near-black or white for a hex color.

## Presets and colors

The default is the **Standard** preset in **Blue on neutral grays**: white cards, tables and fields on
a near-white page. Declare the theme in one place, `IzzyTheme` in `App.razor`'s `<head>`: a preset as
the starting set of defaults, then your brand color, grays, radius and density over it.

```razor
@* App.razor, in <head>, for example: *@
<IzzyTheme Preset="ThemePreset.Compact" />                    @* a preset *@
<IzzyTheme Preset="ThemePreset.Compact" Brand="teal" />       @* a preset in a built-in brand color *@
<IzzyTheme Preset="ThemePreset.Soft" Brand="#0a7d5a" />       @* a preset in your brand color *@
<IzzyTheme Brand="#0a7d5a" Gray="#f4efe6" />                  @* your brand color on your own grays *@
```

| `Preset` (`data-ib-preset`) | For | Controls | Shape | Type | Brand | Gray |
| --- | --- | --- | --- | --- | --- | --- |
| `standard` (default) | Internal tools, line-of-business | 36px | Medium radius, hairline shadow | System font | Blue | Neutral |
| `compact` | Admin, ERP, finance, data grids | 32px, tight rows | Small radius, borders only | System font | Indigo | Slate |
| `soft` | Consumer apps, education, health, portals | 40px, roomy | Large radius, gentle shadow | System font | Teal | Neutral |
| `editorial` | Docs, publishing, knowledge bases | 36px | Large radius | IBM Plex Sans, Newsreader headings | Ink | Stone |

A preset's brand color, gray and radius are only its defaults: `Brand`, `Gray` and `Radius` beat them.

`IzzyTheme` renders meta tags the theme CSS matches (and a small `<style>` block for hex colors, the
fonts and `HeadingWeight`), so it's in place on first paint, needs no script and works in every
render mode. The built-in choices also work as attributes, on `<html>` or on any element for part of
a page (see [Theming part of a page](#theming-part-of-a-page)):

```html
<html lang="en" data-ib-preset="compact" data-ib-brand="teal">
<section data-ib-brand="rose">...</section>
```

**Your brand color.** `Brand` takes a hex color, which becomes the primary accent: `Accent.Primary`
buttons, links, focus rings, the field halo, selected rows, switches, sliders and radios.

```razor
<IzzyTheme Brand="#0a7d5a" />
```

From that one color IzzyBlazor derives the fill, the text on it, the accent as text and its soft
tint, in light and dark (`BrandPalette.From`), and checks each pair: text on the fill holds 4.5:1
(white or near-black, whichever reads better), the accent as text holds 4.5:1 on the surface, the
gray fills and its tint, and in dark mode a dark brand color is lifted until it shows. The fill is
your color as given unless no text can reach 4.5:1 on it; then only its lightness moves. The server
renders it as a small `<style>` block, so it's there on first paint with no script; pass `Nonce` for
a strict Content-Security-Policy.

Or name a built-in one, each tuned by hand so text holds 4.5:1 on its fill and on every gray: `blue`
(the default), `ink` (monochrome), `indigo`, `violet`, `teal`, `green`, `rose`.

To tune the shades yourself, set the four tokens instead:

```css
:root {
  --ib-color-primary:      light-dark(#0a7d5a, #0a7d5a);  /* the fill */
  --ib-color-on-primary:   light-dark(#ffffff, #ffffff);  /* text on the fill */
  --ib-color-primary-text: light-dark(#006e4c, #70cfa7);  /* the accent as text */
  --ib-color-primary-soft: light-dark(#dceae3, #202f2b);  /* the soft tint */
}
```

**Your grays.** `Gray` takes a built-in set, `neutral` (the default), `slate` (cool) or `stone` (warm
paper, which pairs with serif headings), or a hex color for your light page background:

```razor
<IzzyTheme Gray="#f4efe6" />
```

The page is that color, as given (kept light enough for text to read). The rest of the ladder is
derived from it (`GrayPalette.From`): surfaces just above it, the container fills, borders and the
outline stepping down by the same amounts as the built-in sets, in its hue. The container fills are
never darker than the built-in sets' own, so the colors that don't change with the grays (brand and
status text, code) keep their contrast on them: a darker page gets a shallower ladder. Body, muted, subtle and
disabled text, each checked for contrast (muted and subtle hold 4.5:1 on the page, the surface and
the container fill); and the dark scheme in the same hue. A hex gray is app-wide only.

`Brand` and `Gray` are strings because a custom color makes them open sets: anything other than a
known name or a hex color is refused, with a message listing the names.

**Fonts.** Body text uses Inter (`--ib-font-body`), served with IzzyBlazor: variable weight and
optical size, so headings get Inter Display's tighter shapes on their own. Browsers download only
the character sets a page uses (about 70 KB for English), and nothing at all once `BodyFont` or
`--ib-font-body` names another font. Headings, card and dialog titles use
`--ib-font-heading` at `--ib-weight-heading`: by default the body font, semibold. The `editorial`
preset uses IBM Plex Sans and Newsreader; load them in your `<head>` (or self-host them; both are
under the SIL Open Font License):

```html
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600&family=Newsreader:opsz,wght@6..72,500..600&display=swap" />
```

Code, `Kbd` and the color picker's hex box use `--ib-font-mono`, the system's monospace font. Set
your own with `IzzyTheme`'s `BodyFont`, `HeadingFont`, `MonoFont` and `HeadingWeight` (below).

**Radius.** `Radius` shifts the whole scale, so controls, cards and dialogs keep their proportions:
`None` (square), `Sm`, `Md` (the default), `Lg`, or `Full`, which makes controls pills (buttons,
fields, segmented controls, tags and badges; a joined button group rounds only its outer ends) while
cards, popovers and dialogs take the large scale.

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
the sidebar), `Inset`, `Flat` (every part on the surface) or `Canvas`. See
[App layout](components.md#app-layout).

## Customizing

Two levels. `IzzyTheme` takes the choices that move many values together and keeps them right
(contrast, light and dark pairs, whole pixels). CSS variables take single, exact values.

```razor
<IzzyTheme Preset="ThemePreset.Soft" Density="ThemeDensity.Compact" Brand="#0a7d5a" Gray="stone"
           BodyFont="Sora, system-ui, sans-serif" HeadingWeight="700" ColorScheme="ColorScheme.Light" />
```

| `IzzyTheme` / attribute | Sets | Values |
| --- | --- | --- |
| `Preset` / `data-ib-preset` | Density, shape, type and default colors together | `Standard`, `Compact`, `Soft`, `Editorial` |
| `Brand` / `data-ib-brand` | The brand color: fill, text on it, accent text and soft tint, light and dark, each at AA | Hex, or `blue`, `ink`, `indigo`, `violet`, `teal`, `green`, `rose` |
| `Gray` / `data-ib-gray` | Page, surfaces, fills, borders and text | Hex (app-wide), or `neutral`, `slate`, `stone` |
| `Radius` / `data-ib-radius` | Controls, cards and dialogs, in proportion | `None`, `Sm`, `Md`, `Lg`, `Full` |
| `Density` / `data-ib-density` | Control heights, table rows and card padding | `Compact` (32px), `Standard` (36px), `Comfortable` (40px) |
| `BodyFont`, `HeadingFont`, `MonoFont` | Body, heading and code font families (Inter is served with IzzyBlazor; load any other font yourself); app-wide | CSS font-family lists |
| `HeadingWeight` | Headings' weight; app-wide | 100 to 900 |
| `ColorScheme` / `data-ib-color-scheme` | Light or dark until the user picks one with a `ColorSchemeSwitch` | `Auto`, `Light`, `Dark` (and `Inverse` for part of a page) |

Each of `Brand`, `Gray`, `Radius`, `Density` and the fonts beats the preset's own; leave them unset to
take the preset's. Attributes take lowercase values (`data-ib-radius="full"`).

For one exact value, set its variable in your own stylesheet. IzzyBlazor's CSS sits in cascade
layers, so a plain rule wins:

```css
:root {
  --ib-color-focus: #7c3aed;     /* the focus ring and field halo */
  --ib-card-padding: 1.5rem;
  --ib-radius-xl: 0.5rem;        /* cards and other large panels, not the whole scale */
}
```

Every token, by group:

| Group | Tokens |
| --- | --- |
| Page and surfaces | `--ib-color-background`, `-surface`, `-surface-raised`, `-raised-border` (a floating surface's edge in dark mode), `-surface-container-low`, `-surface-container`, `-surface-container-high` |
| Text and lines | `--ib-color-on-surface`, `-muted`, `-subtle`, `-disabled`, `-border`, `-outline`, `-backdrop` (behind dialogs) |
| Brand | `--ib-color-primary`, `-on-primary`, `-primary-text`, `-primary-soft` |
| Status | `--ib-color-success`, `-warning`, `-danger`, `-info`, `-neutral`, each with `-on-` (text on it); the first four with `-text` and `-soft` |
| Made from other colors | `--ib-color-focus` (focus ring and halo; primary), `--ib-color-halo`, `--ib-color-selected` (a selected row), `--ib-state-hover` and `--ib-state-pressed` (washes of the text color), `--ib-color-segment` and `-segment-edge` (the chosen segment), `--ib-color-scrollbar` and `-scrollbar-hover`. Unset by default: each is worked out where it's used, so it follows colors set on part of the page; set it to replace the calculation |
| Shape | `--ib-radius-none`, `-sm`, `-md`, `-lg`, `-xl`, `-full`; `--ib-radius-control` (buttons, fields, segmented controls, tags; unset: `-md`) and `--ib-radius-control-sm` (badges; unset: `-sm`) |
| Density | `--ib-control-h-sm`, `-md`, `-lg` (buttons, fields, tabs, pagination, nav items, menu rows), `--ib-control-font-sm` (text in small controls, 13px), `--ib-control-icon-sm`, `-md`, `-lg` (icons in controls, 16, 20 and 24px) and `--ib-control-icon-stroke-sm` (their stroke at 16px, 2.5 units), `--ib-table-cell-py`, `--ib-card-padding`, the spacing scale `--ib-space-0` to `-16` |
| Sizes | `--ib-size-sm`, `-md`, `-lg`, `-xl` (640 to 1280px), `--ib-size-prose` (a readable line) |
| Type | `--ib-font-body`, `-heading`, `-mono`; `--ib-text-xs` to `-6xl`; `--ib-weight-light`, `-normal`, `-medium`, `-semibold`, `-bold`, `-extrabold`, `-heading`; `--ib-leading-none`, `-tight`, `-snug`, `-normal`, `-relaxed`, `-loose`; `--ib-tracking-tighter` to `-widest` |
| Depth | `--ib-shadow-none`, `-xs` (under cards), `-sm`, `-md`, `-lg` |
| Motion | `--ib-duration-fast`, `--ib-duration-normal`, `--ib-ease` |
| Code | `--ib-code-comment`, `-keyword`, `-string`, `-number`, `-type`, `-function`, `-tag`, `-attr`, `-meta` (syntax colors, each at least 4.5:1 on the code block) |

**Component variables.** A few components have their own variables for the things people tune
most. Like the tokens, each works on `:root` for the whole app or on any element for part of it.
Anything not listed here, including class names (`.ib-*`), is internal and may change; to style
your own way, add a class with `Class`.

| Component | Variables |
| --- | --- |
| Button, LinkButton, IconButton | `--ib-btn-px` (side padding, every size), `--ib-btn-radius`; heights are `--ib-control-h-*` |
| Fields | `--ib-field-radius`; heights are `--ib-control-h-*` |
| Card | `--ib-card-padding` |
| Table | `--ib-table-cell-py`, `--ib-table-cell-px` (medium size) |
| AppShell | `--ib-shell-{header,sidebar,main,aside,footer}-bg` and `-fg` (each part's colors), `--ib-shell-divider` (the lines between parts), `--ib-sidebar-w`, `--ib-aside-w`, `--ib-shell-header-h` and `--ib-shell-footer-h` (room kept for sticky bars), `--ib-banner-h` (a sticky banner's height) |
| AvatarGroup | `--ib-avatar-ring` (the ring between avatars; the page color by default) |
| Timeline | `--ib-timeline-bg` (behind the markers; the surface by default) |

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
  --ib-font-body: "IBM Plex Sans", system-ui, sans-serif;
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
(`ColorScheme.Light`, `Dark` or `Auto`), a [`ColorSchemeSwitch`](components.md#colorschemeswitch)
lets the user choose (kept in the `ib-scheme` cookie, which `IzzyTheme` reads on the server with
`AddIzzyBlazor()`, so pages arrive in the chosen scheme with no flash), and
`<html data-ib-color-scheme="light|dark">` works in plain HTML.

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

## Theming part of a page

For more than a scheme, wrap the part in an `IzzyTheme`. With content it themes what's inside, the
way Ant Design's nested `ConfigProvider` and Radix's nested `Theme` do, and what it doesn't set comes
from the theme around it:

```razor
<IzzyTheme Density="ThemeDensity.Compact" Brand="#be123c" Style="--ib-card-padding: 1rem">
    <Table ... />
</IzzyTheme>
```

It takes `Brand` (a name or hex), a built-in `Gray`, `Radius`, `Density`, `Preset` and `ColorScheme`,
and renders a `div` (with `Class`, `Style` and other attributes). On part of a page a preset changes
density, shape and depth (the card shadow) only; its fonts, brand color and gray apply to the whole
app alone. Fonts,
`HeadingWeight` and a hex `Gray` are app-wide: set them on the `IzzyTheme` in `<head>`.

`IzzyTheme` takes only the choices it keeps consistent. A single exact value is CSS: put it in
`Style`, or in a class (`Class="report-band"`) with the value in your stylesheet.

Everything inside follows the part's colors, including the colors made from them: the focus ring,
the field halo, selected rows, hover and pressed washes, the chosen segment and scrollbar thumbs are
worked out where they're used. So does a plain CSS override (`section { --ib-color-primary: … }`).
Each of those has its own token too (`--ib-color-focus`, `--ib-color-halo`, `--ib-color-selected`,
`--ib-state-hover`, `--ib-state-pressed`, `--ib-color-segment`, `--ib-color-scrollbar`) if you want
to set it outright.

The same choices work as attributes in plain HTML: `data-ib-brand`, `data-ib-gray`, `data-ib-radius`,
`data-ib-density` and `data-ib-preset` on any element. The more specific choice always wins: an
attribute beats `IzzyTheme`'s app setting, and a brand color or density beats a preset's.

Menus, popovers, tooltips and declared `Dialog`s stay where they're written in the page (the top
layer only moves them on screen), so they take on the part's theme. Dialogs opened from code with
`DialogService` are shown by the `DialogHost`, so pass `DialogOptions.ColorScheme`, `Brand` and
`Density` to match the part that opened them.

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
