# IzzyBlazor brand assets

All text is outlined, so the SVGs need no fonts installed.

## What's where

| Folder | Files | Use |
| --- | --- | --- |
| `logo/` | `izzyblazor-wordmark-{light,dark,mono-ink,mono-white}.svg` | Main logo. `light` goes on light backgrounds, `dark` on dark ones. Mono for print, embossing, single-color contexts. |
| `logo/` | `izzyblazor-wordmark-{light,dark}-small.svg` | Wordmark without the halo, for site headers and anything under about 48 px tall. |
| `logo/` | `izzyblazor-icon.svg`, `-icon-small.svg`, `-icon-tiny.svg` | The knob on an ink disc. Use `icon` at 48 px and up, `small` at 24 to 47 px, `tiny` below 24 px. |
| `logo/` | `izzyblazor-icon-mono-{ink,white}.svg`, `izzyblazor-knob.svg` | One-color knob (ring and core), and the knob without a disc. |
| `logo/png/` | PNG exports of the above | For tools that don't take SVG. |
| `favicon/` | `favicon.ico`, `favicon.svg`, `apple-touch-icon.png`, `icon-192.png`, `icon-512.png`, `icon-512-maskable.png`, `site.webmanifest`, `head-snippet.html` | Drop into `wwwroot/` and paste the snippet into `<head>`. |
| `github/` | `avatar-500.png` | GitHub org or repo avatar. |
| `github/` | `social-preview.png` (1280×640) | Repo Settings → Social preview. |
| `github/` | `readme-banner-{light,dark}.{svg,png}` | Top of the README (see snippet below). Transparent background. |
| `social/` | `og-image.png` (1200×630) | Open Graph / X card image for the docs site. |
| `social/` | `banner-1500x500.png` | X and LinkedIn header. |
| `hero/` | `hero-dark.{svg,png}` (1600×800, PNG at 2x) | Docs landing page, video intros, slides. |
| `nuget/` | `icon.png` (128×128) | `<PackageIcon>` in the .csproj. |
| `badges/` | `mode-{off,static,live}-{light,dark}.{svg,png}` | The Off · Static · Live mode badge for demos and thumbnails. |

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
- **Halo** at 48 px and up. **Amber core** at 24 px and up. Below that, a plain ember dot.
- **Clear space** around the logo equals the knob's diameter.
- Don't recolor the knob, stretch the wordmark, or set "izzyblazor" in another font. No Blazor or .NET logos alongside it.

## Colors and type

| Name | Hex | Use |
| --- | --- | --- |
| Ink | `#14161A` | Dark backgrounds, "izzy" on light |
| Paper | `#F5F4F1` | Light backgrounds, "izzy" on dark |
| Ember | `#F26A1B` | The knob, accents |
| Amber | `#FFB020` | Knob core, highlights |
| Steel | `#8A8F98` | Muted UI |
| Grey (light) / Grey (dark) | `#5A6069` / `#A9AEB6` | "blazor" in the wordmark, secondary text |
| Ember text | `#B8440A` | Orange links and small type on light backgrounds |

Fonts: Bricolage Grotesque (display), JetBrains Mono (code). Both are SIL Open Font License.

## Regenerating

`source/build.py` rebuilds everything. Needs Python with `uharfbuzz`, `resvg-py`, `fonttools`, `Pillow`, and the fonts from `npm i @fontsource/bricolage-grotesque @fontsource/jetbrains-mono`.
