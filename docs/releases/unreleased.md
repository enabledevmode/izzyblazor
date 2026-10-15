Icons now come from Tabler Icons instead of Lucide: the same 24px outline style, nearly three
times the icons, filled versions of about a thousand of them, and brand logos that match. Nothing about
how you use an icon changes, only the class and some names, so a build error points to every place
to update.

## Renamed

| Before | Now |
| --- | --- |
| `Lucide.X` (any icon) | `Tabler.X` when Tabler uses the same name (over half of the icons IzzyBlazor used); the table below lists the rest |
| `IconSet.Lucide` | `IconSet.Tabler` |

Names that differ, among the icons IzzyBlazor's docs and demos used. For any other icon, search
https://tabler.io/icons.

| Lucide | Tabler |
| --- | --- |
| `Lucide.Banknote` | `Tabler.Cash` |
| `Lucide.BookOpen` | `Tabler.Book` |
| `Lucide.ChevronsUpDown` | `Tabler.Selector` |
| `Lucide.CircleAlert` | `Tabler.AlertCircle` |
| `Lucide.CircleUser` | `Tabler.UserCircle` |
| `Lucide.ClipboardPaste` | `Tabler.Clipboard` |
| `Lucide.DollarSign` | `Tabler.CurrencyDollar` |
| `Lucide.Ellipsis` | `Tabler.Dots` |
| `Lucide.FolderGit2` | `Tabler.FolderCode` |
| `Lucide.FolderInput` | `Tabler.FolderSymlink` |
| `Lucide.FolderKanban` | `Tabler.LayoutKanban` |
| `Lucide.GitCommitHorizontal` | `Tabler.GitCommit` |
| `Lucide.Hand` | `Tabler.HandStop` |
| `Lucide.House` | `Tabler.Home` |
| `Lucide.Info` | `Tabler.InfoCircle` |
| `Lucide.ListChecks` | `Tabler.ListCheck` |
| `Lucide.LogIn` | `Tabler.Login` |
| `Lucide.LogOut` | `Tabler.Logout` |
| `Lucide.MousePointerClick` | `Tabler.Click` |
| `Lucide.NotebookPen` | `Tabler.Notebook` |
| `Lucide.PackageCheck` | `Tabler.Package` |
| `Lucide.PanelBottom` | `Tabler.LayoutBottombar` |
| `Lucide.PanelLeft` | `Tabler.LayoutSidebar` |
| `Lucide.PanelRight` | `Tabler.LayoutSidebarRight` |
| `Lucide.PanelTop` | `Tabler.LayoutNavbar` |
| `Lucide.Play` | `Tabler.PlayerPlay` |
| `Lucide.RefreshCw` | `Tabler.Refresh` |
| `Lucide.RotateCcw` | `Tabler.Rotate` |
| `Lucide.Save` | `Tabler.DeviceFloppy` |
| `Lucide.SearchX` | `Tabler.SearchOff` |
| `Lucide.Shapes` | `Tabler.Shape` |
| `Lucide.SlidersHorizontal` | `Tabler.AdjustmentsHorizontal` |
| `Lucide.Sprout` | `Tabler.Seedling` |
| `Lucide.Store` | `Tabler.BuildingStore` |
| `Lucide.TextAlignCenter` | `Tabler.AlignCenter` |
| `Lucide.TextAlignEnd` | `Tabler.AlignRight` |
| `Lucide.TextAlignJustify` | `Tabler.AlignJustified` |
| `Lucide.TextAlignStart` | `Tabler.AlignLeft` |
| `Lucide.TextCursorInput` | `Tabler.CursorText` |
| `Lucide.Timer` | `Tabler.Stopwatch` |
| `Lucide.TriangleAlert` | `Tabler.AlertTriangle` |
| `Lucide.UsersRound` | `Tabler.Users` |
| `Lucide.Zap` | `Tabler.Bolt` |

## Changed

- **`Brands` are outlines.** The same 13 names (`Brands.GitHub`, `Brands.YouTube` and the rest), now
  drawn as outlines that match the icons beside them instead of solid Simple Icons shapes. Tabler has
  hundreds more as `Tabler.Brand*`. NuGet and .NET, which Tabler doesn't have, were drawn for
  IzzyBlazor to match.
- **Components no longer name an icon set.** The color scheme switch, Avatar's empty state,
  VideoEmbed's play button and FileUpload's default icon now ask `IconSet.Current` like every other
  component, through six new roles: `SchemeLight`, `SchemeDark`, `SchemeSystem`, `Person`, `Play`
  and `Upload`.
- The menu icon (`IconRole.Menu`) is Tabler's three-line `Menu2`, matching the old one.
- **Icons in controls are sized by the control.** Buttons, icon buttons, fields, segmented controls,
  tabs, pagination and nav items use 16, 20 or 24px icons for small, medium and large, about 1.4
  times the label, instead of the label's own size (14px in a medium button). Line icons drawn on a
  24px grid look soft when shrunk to the text on a standard-density screen; these sizes stay crisp.
  Menu rows are small controls (16px). Icons at 16px or less, in small controls and tags, draw their
  stroke at 2.5 units so it isn't faint; an icon given its own `StrokeWidth` keeps it, and a `Size`
  still wins over the control's. The sizes are `--ib-control-icon-sm`, `-md` and `-lg`.

## New

- **`TablerFilled`**: solid versions of 1,054 icons, for a selected or active state, such as
  `TablerFilled.Star` for a starred item.
- The dev host's Icon page browses outline or filled icons and shows the filled pairs.

## Size

The icon classes are bigger: `IzzyBlazor.Core.dll` grows from 1.45 MB to 4.31 MB (281 KB to 553 KB
with Brotli). Server-rendered apps don't download it. WebAssembly apps do, and they keep every icon
until IzzyBlazor.Core is marked trimmable, which needs its trim warnings fixed first.
