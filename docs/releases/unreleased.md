IzzyBlazor now starts from a modern font and icon set: icons come from the free Hugeicons set
(Stroke Rounded) instead of Lucide, and the default body font is Inter, served with IzzyBlazor. Nothing
about how you use an icon changes, only the class and the names, so a build error points to every
place to update.

## Renamed

| Before | Now |
| --- | --- |
| `Lucide.X` (any icon) | `Hugeicons.Y`: Hugeicons' names, numbered where it has several takes on an idea; the table below lists the icons IzzyBlazor's docs and demos used |
| `IconSet.Lucide` | `IconSet.Hugeicons` |

For any other icon, search https://hugeicons.com/icons (Stroke Rounded, Free) or the dev host's Icon
page. In IntelliSense each icon lists its other names, so `Delete02` shows Trash.

| Lucide | Hugeicons |
| --- | --- |
| `Lucide.Activity` | `Hugeicons.Activity01` |
| `Lucide.Archive` | `Hugeicons.Archive03` |
| `Lucide.ArrowDown` | `Hugeicons.ArrowDownNarrowWide` |
| `Lucide.ArrowDownRight` | `Hugeicons.ArrowDownRight01` |
| `Lucide.ArrowLeft` | `Hugeicons.ArrowLeft02` |
| `Lucide.ArrowRight` | `Hugeicons.ArrowRight02` |
| `Lucide.ArrowUp` | `Hugeicons.ArrowUpNarrowWide` |
| `Lucide.ArrowUpRight` | `Hugeicons.ArrowUpRight01` |
| `Lucide.ArrowsSort` | `Hugeicons.ArrowUpDown` |
| `Lucide.Banknote` | `Hugeicons.Cash01` |
| `Lucide.Bell` | `Hugeicons.Notification01` |
| `Lucide.Bold` | `Hugeicons.TextBold` |
| `Lucide.BookOpen` | `Hugeicons.Book03` |
| `Lucide.Bookmark` | `Hugeicons.Bookmark02` |
| `Lucide.Calendar` | `Hugeicons.Calendar03` |
| `Lucide.ChartColumn` | `Hugeicons.ChartColumn` |
| `Lucide.Check` | `Hugeicons.Check` |
| `Lucide.ChevronDown` | `Hugeicons.ChevronDown` |
| `Lucide.ChevronLeft` | `Hugeicons.ChevronLeft` |
| `Lucide.ChevronRight` | `Hugeicons.ChevronRight` |
| `Lucide.ChevronUp` | `Hugeicons.ChevronUp` |
| `Lucide.ChevronsUpDown` | `Hugeicons.UnfoldMore` |
| `Lucide.CircleAlert` | `Hugeicons.AlertCircle` |
| `Lucide.CircleCheck` | `Hugeicons.CircleCheck` |
| `Lucide.CircleUser` | `Hugeicons.UserCircle` |
| `Lucide.ClipboardPaste` | `Hugeicons.ClipboardPaste` |
| `Lucide.Clock` | `Hugeicons.Clock01` |
| `Lucide.CloudUpload` | `Hugeicons.CloudUpload` |
| `Lucide.Columns2` | `Hugeicons.Layout2Column` |
| `Lucide.Copy` | `Hugeicons.Copy` |
| `Lucide.CopyCheck` | `Hugeicons.CopyCheck` |
| `Lucide.CreditCard` | `Hugeicons.CreditCard` |
| `Lucide.DeviceDesktop` | `Hugeicons.Computer` |
| `Lucide.DollarSign` | `Hugeicons.Dollar01` |
| `Lucide.Download` | `Hugeicons.Download01` |
| `Lucide.Ellipsis` | `Hugeicons.MoreHorizontal` |
| `Lucide.ExternalLink` | `Hugeicons.ExternalLink` |
| `Lucide.Eye` | `Hugeicons.View` |
| `Lucide.EyeOff` | `Hugeicons.ViewOffSlash` |
| `Lucide.Filter` | `Hugeicons.Filter` |
| `Lucide.Folder` | `Hugeicons.Folder01` |
| `Lucide.FolderGit2` | `Hugeicons.FolderCode` |
| `Lucide.FolderInput` | `Hugeicons.FolderExport` |
| `Lucide.FolderKanban` | `Hugeicons.Kanban` |
| `Lucide.GitBranch` | `Hugeicons.GitBranch` |
| `Lucide.GitCommitHorizontal` | `Hugeicons.GitCommit` |
| `Lucide.Globe` | `Hugeicons.Globe02` |
| `Lucide.Hand` | `Hugeicons.Hand` |
| `Lucide.Hash` | `Hugeicons.Hash` |
| `Lucide.Heart` | `Hugeicons.Favourite` |
| `Lucide.House` | `Hugeicons.Home03` |
| `Lucide.Inbox` | `Hugeicons.Inbox` |
| `Lucide.Info` | `Hugeicons.InformationCircle` |
| `Lucide.Italic` | `Hugeicons.TextItalic` |
| `Lucide.LayoutDashboard` | `Hugeicons.DashboardSquare01` |
| `Lucide.Link` | `Hugeicons.Link04` |
| `Lucide.ListChecks` | `Hugeicons.CheckList` |
| `Lucide.Loader2` | `Hugeicons.Loader` |
| `Lucide.LogIn` | `Hugeicons.Login01` |
| `Lucide.LogOut` | `Hugeicons.Logout01` |
| `Lucide.Mail` | `Hugeicons.Mail01` |
| `Lucide.Menu2` | `Hugeicons.Menu01` |
| `Lucide.Minus` | `Hugeicons.Minus` |
| `Lucide.Moon` | `Hugeicons.Moon02` |
| `Lucide.MousePointerClick` | `Hugeicons.CursorPointer01` |
| `Lucide.NotebookPen` | `Hugeicons.Notebook` |
| `Lucide.PackageCheck` | `Hugeicons.Package` |
| `Lucide.Palette` | `Hugeicons.Palette` |
| `Lucide.PanelBottom` | `Hugeicons.LayoutBottom` |
| `Lucide.PanelLeft` | `Hugeicons.LayoutLeft` |
| `Lucide.PanelRight` | `Hugeicons.LayoutRight` |
| `Lucide.PanelTop` | `Hugeicons.LayoutTop` |
| `Lucide.Pencil` | `Hugeicons.PencilEdit01` |
| `Lucide.Play` | `Hugeicons.Play` |
| `Lucide.Plug` | `Hugeicons.Plug01` |
| `Lucide.Plus` | `Hugeicons.Plus` |
| `Lucide.Receipt` | `Hugeicons.Invoice01` |
| `Lucide.RefreshCw` | `Hugeicons.Refresh` |
| `Lucide.Rocket` | `Hugeicons.Rocket01` |
| `Lucide.RotateCcw` | `Hugeicons.RotateClockwise` |
| `Lucide.Save` | `Hugeicons.FloppyDisk` |
| `Lucide.Scissors` | `Hugeicons.Scissor` |
| `Lucide.Search` | `Hugeicons.Search01` |
| `Lucide.SearchX` | `Hugeicons.SearchRemove` |
| `Lucide.Send` | `Hugeicons.Send` |
| `Lucide.Server` | `Hugeicons.ServerStack01` |
| `Lucide.Settings` | `Hugeicons.Settings01` |
| `Lucide.Shapes` | `Hugeicons.Shapes01` |
| `Lucide.Share2` | `Hugeicons.Share08` |
| `Lucide.Shield` | `Hugeicons.ShieldCheck` |
| `Lucide.ShoppingCart` | `Hugeicons.ShoppingCart02` |
| `Lucide.SlidersHorizontal` | `Hugeicons.SlidersHorizontal` |
| `Lucide.Sparkles` | `Hugeicons.Sparkles` |
| `Lucide.Sprout` | `Hugeicons.Plant01` |
| `Lucide.Star` | `Hugeicons.Star` |
| `Lucide.Store` | `Hugeicons.Store02` |
| `Lucide.Sun` | `Hugeicons.Sun03` |
| `Lucide.TextAlignCenter` | `Hugeicons.TextAlignCenter` |
| `Lucide.TextAlignEnd` | `Hugeicons.TextAlignRight` |
| `Lucide.TextAlignJustify` | `Hugeicons.TextAlignJustify` |
| `Lucide.TextAlignStart` | `Hugeicons.TextAlignLeft` |
| `Lucide.TextCursorInput` | `Hugeicons.CursorText` |
| `Lucide.Timer` | `Hugeicons.Timer02` |
| `Lucide.Trash` | `Hugeicons.Delete02` |
| `Lucide.TriangleAlert` | `Hugeicons.Alert02` |
| `Lucide.Truck` | `Hugeicons.DeliveryTruck01` |
| `Lucide.Underline` | `Hugeicons.TextUnderline` |
| `Lucide.Upload` | `Hugeicons.Upload01` |
| `Lucide.User` | `Hugeicons.User02` |
| `Lucide.UserPlus` | `Hugeicons.UserAdd02` |
| `Lucide.UsersRound` | `Hugeicons.UserGroup02` |
| `Lucide.X` | `Hugeicons.Cancel01` |
| `Lucide.Zap` | `Hugeicons.Flash` |
| `Lucide.ZoomIn` | `Hugeicons.ZoomIn` |
| `Lucide.ZoomOut` | `Hugeicons.ZoomOut` |

## Changed

- **Icons are lighter.** Hugeicons draw a 1.5-unit stroke on a 24px grid, so lines are 1px at 16px,
  1.25px at 20px and 1.5px at 24px: light enough to sit with regular text, and close to whole pixels
  on a standard-density screen. `StrokeWidth` still changes it; set `--ib-icon-stroke` on any element
  to change it for every default-weight icon inside.
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
  Hugeicons' outline logos instead of solid Simple Icons shapes, so they match the icons beside them.
  NuGet and .NET, which Hugeicons doesn't have, were drawn for IzzyBlazor to match. Hugeicons has more,
  such as `Hugeicons.Linkedin01` and `Hugeicons.Twitch`.
- **Components no longer name an icon set.** The color scheme switch, Avatar's empty state,
  VideoEmbed's play button and FileUpload's default icon now ask `IconSet.Current` like every other
  component, through six new roles: `SchemeLight`, `SchemeDark`, `SchemeSystem`, `Person`, `Play`
  and `Upload`.

## Size

The icon class is bigger: `IzzyBlazor.Core.dll` grows from 1.45 MB to 7.9 MB (281 KB to about 1 MB
with Brotli), since Hugeicons draw with more detail than Lucide. Server-rendered apps don't download it.
WebAssembly apps do, and they keep every icon until IzzyBlazor.Core is marked trimmable, which needs
its trim warnings fixed first. The Inter files add 724 KB to the package, of which a page downloads
only the character sets it uses.
