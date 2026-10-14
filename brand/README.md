# IzzyBlazor brand assets

All text is outlined, so the SVGs need no fonts installed.

## What's where

| Folder | Files | Use |
| --- | --- | --- |
| `logo/` | `izzyblazor-wordmark-{light,dark,mono-ink,mono-white}.svg` | Main logo. `light` goes on light backgrounds, `dark` on dark ones. Mono for print, embossing, single-color contexts. |
| `logo/` | `izzyblazor-wordmark-{light,dark}-small.svg` | Wordmark without the halo, for site headers and anything under about 48 px tall. |
| `logo/` | `izzyblazor-monogram-{light,dark}.svg` | The "ib" monogram: ink or paper i, ember b, transparent. Light for light backgrounds, dark for dark. |
| `logo/` | `izzyblazor-monogram-fallback.svg` | Steel-grey i, for places that can't switch with the theme (the .ico, Safari). |
| `logo/` | `izzyblazor-monogram-tile.svg` | Monogram on a plain ink tile, for avatars and app icons. |
| `logo/` | `izzyblazor-monogram-mono-{ink,white}.svg`, `izzyblazor-knob.svg` | One-color monogram, and the glowing knob on its own (the dot from the wordmark). |
| `logo/png/` | PNG exports of the above | For tools that don't take SVG. |
| `favicon/` | `favicon.ico`, `favicon.svg`, `favicon-{16,32,48}.png`, `apple-touch-icon.png`, `icon-192.png`, `icon-512.png`, `icon-512-maskable.png`, `site.webmanifest`, `head-snippet.html` | Drop into `wwwroot/` and paste the snippet into `<head>`. In `favicon.svg` the i switches between ink and paper with the browser's light or dark theme. |
| `github/` | `avatar-500.png` | GitHub org or repo avatar. The letters stay inside the circle GitHub crops to. |
| `github/` | `social-preview.{svg,png}` (1280×640) | Repo Settings → Social preview. |
| `github/` | `readme-banner-{light,dark}.{svg,png}` | Top of the README (see snippet below). Transparent background. |
| `social/` | `og-image.{svg,png}` (1200×630) | Open Graph / X card image for the docs site. |
| `social/` | `banner-1500x500.{svg,png}` | X and LinkedIn header. |
| `hero/` | `hero-dark.{svg,png}` (1600×800, PNG at 2x) | Docs landing page, video intros, slides. |
| `nuget/` | `icon.png` (128×128) | `<PackageIcon>` in the .csproj. |
| `badges/` | `mode-{off,static,live}-{light,dark}.svg`, PNGs in `png/` | The Off · Static · Live mode badge for demos and thumbnails. |

## README header

```html
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="brand/github/readme-banner-dark.svg">
    <img alt="IzzyBlazor: Blazor components. No circuit required." src="brand/github/readme-banner-light.svg" width="640">
  </picture>
</p>
```

## NuGet icon

```xml
<PropertyGroup>
  <PackageIcon>icon.png</PackageIcon>
</PropertyGroup>
<ItemGroup>
  <None Include="brand/nuget/icon.png" Pack="true" PackagePath="\" />
</ItemGroup>
```

## Rules

- **Tagline:** "Blazor components. No circuit required." **Subhead:** "Works in static SSR. No SignalR or WebAssembly needed."
- **Wordmark:** the dot gets its halo at 48 px and up and its amber core at 24 px and up. Below that it's a plain ember dot.
- **Icon:** use the "ib" monogram wherever a square mark is needed. It's transparent at favicon sizes, and sits on a plain ink tile at 128 px and up.
- **Clear space** around the logo equals the knob's diameter.
- Don't recolor the knob or the b, stretch the wordmark, or set "izzyblazor" in another font. No Blazor or .NET logos alongside it.

## Colors and type

| Name | Hex | Use |
| --- | --- | --- |
| Ink | `#14161A` | Dark backgrounds, "izzy" and the monogram i on light |
| Paper | `#F5F4F1` | Light backgrounds, "izzy" and the monogram i on dark |
| Ember | `#F26A1B` | The knob, the monogram b, accents |
| Amber | `#FFB020` | Knob core, highlights |
| Steel | `#8A8F98` | Muted UI, fallback monogram i |
| Grey (light) / Grey (dark) | `#5A6069` / `#A9AEB6` | "blazor" in the wordmark, secondary text |
| Ember text | `#B8440A` | Orange links and small type on light backgrounds |

Fonts: Bricolage Grotesque (display), JetBrains Mono (code). Both are SIL Open Font License.

## Regenerating

`source/build.py` rebuilds everything. Needs Python with `uharfbuzz`, `resvg-py`, `fonttools`, `Pillow`, and the fonts from `npm i @fontsource/bricolage-grotesque @fontsource/jetbrains-mono`.
