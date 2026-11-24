# IzzyBlazor VS Code extension icons

| File | Use |
| --- | --- |
| `icon.png` (256×256) | The extension icon, shown in the Extensions view and on the Marketplace. The "ib" monogram on the ink tile, the same mark as the NuGet icon and app icons. |
| `icon.svg` | Source for `icon.png`. VS Code needs the PNG for `"icon"`. |
| `activitybar.svg` (24×24) | The Activity Bar (sidebar) entry: "ib" inside an outlined tile, drawn with a 1.5px line to match VS Code's own line icons. |
| `activitybar-letters.svg` (24×24) | Alternative Activity Bar icon: the "ib" letters alone. Bolder and heavier than the built-in icons. |

## package.json

```json
{
  "icon": "images/icon.png",
  "galleryBanner": { "color": "#14161A", "theme": "dark" },
  "contributes": {
    "viewsContainers": {
      "activitybar": [
        { "id": "izzyblazor", "title": "IzzyBlazor", "icon": "images/activitybar.svg" }
      ]
    },
    "views": {
      "izzyblazor": [
        { "id": "izzyblazor.catalog", "name": "Catalog", "icon": "images/activitybar.svg" }
      ]
    }
  }
}
```

## How VS Code uses them

- **Two icons, not one.** `"icon"` is the colored extension icon. The Activity Bar icon is separate and one color only.
- **Activity Bar color:** VS Code uses the SVG as a mask and paints it with the theme's icon color, so the fill in the file doesn't matter. It dims it to 60% when inactive and shows it at full strength on hover or when active. The file is 24×24 and VS Code scales it to fit.
- **Extension icon:** at least 128×128 PNG, 256×256 for high-density screens.
- **Gallery banner:** ink (`#14161A`) with light text, so the Marketplace header matches the tile.
- **Commands and view titles:** use built-in codicons (`$(add)`, `$(refresh)`, `$(search)`), not brand marks. Only the Activity Bar entry carries the brand.
- **Marketplace README:** `vsce` rejects SVG images and plain http links in the README. Use PNGs over https, such as `brand/github/readme-banner-light.png`.

## Rules

- Don't recolor the ember "b" or the tile in `icon.png`.
- The Activity Bar icon stays one color. Never add the ember there; VS Code would mask it away anyway.
