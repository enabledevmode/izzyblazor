IzzyBlazor now starts from a sharper, quieter look: Lucide icons are drawn with a lighter 1.5-unit
stroke and sized to the control they sit in, and the default body font is Inter, served with
IzzyBlazor. Icon names don't change.

## Changed

- **Icons are lighter and crisper.** IzzyBlazor draws Lucide with a 1.5-unit stroke instead of
  Lucide's default 2, so lines are 1px at 16px, 1.25px at 20px and 1.5px at 24px: close to whole
  pixels on a standard-density screen, and light enough to sit with regular text. `StrokeWidth`
  still changes it for one icon; set `--ib-icon-stroke` on any element to change it for every
  default-weight icon inside.
- **Icons in controls are sized by the control.** Buttons, icon buttons, fields, segmented controls,
  tabs, pagination and nav items use 16, 20 or 24px icons for small, medium and large, about 1.4
  times the label, instead of the label's own size (14px in a medium button). Menu rows are small
  controls (16px). A `Size` still wins over the control's. The sizes are `--ib-control-icon-sm`,
  `-md` and `-lg`.
- **The default body font is Inter**, served from `_content/IzzyBlazor.Core/fonts` with variable
  weight and optical size, so headings get Inter Display's tighter shapes on their own. Browsers
  download only the character sets a page uses (about 70 KB for English), and nothing once
  `BodyFont` or `--ib-font-body` names another font. It was the system font, which looked different
  on every platform.
- **`Brands` are outlines.** The same 13 names (`Brands.GitHub`, `Brands.YouTube` and the rest), now
  drawn as outlines that match the icons beside them instead of solid Simple Icons shapes: Tabler's
  brand icons, since Lucide no longer includes brands, with NuGet and .NET drawn for IzzyBlazor.
- **Sorted table columns** show `ArrowUpNarrowWide` and `ArrowDownNarrowWide` instead of plain
  arrows, so the sort direction reads at a glance.
- **Components no longer name an icon set.** The color scheme switch, Avatar's empty state,
  VideoEmbed's play button and FileUpload's default icon now ask `IconSet.Current` like every other
  component, through six new roles: `SchemeLight`, `SchemeDark`, `SchemeSystem`, `Person`, `Play`
  and `Upload`.
- Lucide is updated to 1.57.0 (1,890 icons).

## Size

The Inter files add 724 KB to the package, of which a page downloads only the character sets it uses.
