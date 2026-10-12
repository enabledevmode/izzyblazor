# Components

What each component does, and how it behaves with JavaScript off, with JavaScript, and on
interactive pages. Every component has a live demo in the dev host, in both static and interactive
modes (see [Contributing](../CONTRIBUTING.md)).

- [Layout](#layout)
- [Typography, links and code](#typography-links-and-code)
- [Icons](#icons)
- [Buttons](#buttons)
- [Forms](#forms)
- [App layout](#app-layout)
- [Disclosure](#disclosure)
- [Overlays](#overlays)
- [Tabs](#tabs)
- [Breadcrumb and Pagination](#breadcrumb-and-pagination)
- [Alerts and toasts](#alerts-and-toasts)
- [Loading: Spinner, Progress, Skeleton](#loading-spinner-progress-skeleton)
- [Avatar](#avatar)
- [Card](#card)
- [Table](#table)
- [Filtering](#filtering)
- [Open state from C#](#open-state-from-c)

## Layout

`Box` is a plain block. `Row` lays children out horizontally and `Column` vertically, always.
`Stack` is the general flex container: vertical by default, any direction with `Direction`, which
can change per breakpoint (`new() { Base = Direction.Column, Md = Direction.Row }`). Use `Row` or
`Column` when the direction never changes, and `Stack` when it does. Setting `Direction` on a `Row`
or `Column` is a build error. All three take `Gap`, `Align`, `Justify`, `Wrap` and `Inline`, and
all four take the [style props](styling.md).

`Grid` lays out columns in one of three ways:

- `Columns` per breakpoint: `new() { Base = 1, Sm = 2, Lg = 4 }`.
- `MinChildWidth`, to fit as many columns as there's room for. `Stretch` lets a short row fill the width.
- `Template`, for your own column sizes per breakpoint: `"15rem minmax(0, 1fr)"`.

`GridItem` spans columns and rows. For content-sized items that flow, use `Stack` or `Row` with `Wrap`.

The rest:

- `Container` centers content at a readable width (`Sm` to `Xl`, or `Full`).
- `Center` centers both ways.
- `Spacer` pushes the rest of a row to the end.
- `AspectRatio` keeps media at a shape (16:9 by default).
- `Divider` draws a line: plain, dashed, labelled ("or") or vertical.

## Typography, links and code

`TextBlock` (body text, a `p` by default) and `Heading` (`Level` 1 to 6) take every style prop plus:

- `Size`, `Weight`, `Family`, `Align`, `LineHeight`, `LetterSpacing`,
- `Transform`, `Decoration`, `Italic`,
- `Wrap` (including `TextWrap.Balance` and `Pretty`), `Truncate`, `LineClamp`, `TabularNums`.

`Size` and `Align` are responsive. Text components have no default margins; space them with a
Stack's `Gap`. (It's `TextBlock`, not `Text`, because Razor reserves `<text>` in code blocks.)

`Link` is underlined by default (`Underline` changes it). `Inherit` takes the text color, and
`External` opens a new tab with an icon and a note for screen readers.

`Code` is inline, or a `Block` that scrolls sideways with an optional `Language` label. Its copy
button appears only when JavaScript runs.

## Icons

`<Icon Name="Lucide.House" />` renders inline SVG on the server. There's no icon font, it works on
static pages, and it follows the text color (`currentColor`) and size (`em`).

- All 1,877 Lucide icons are bundled as a typed `Lucide` class with IntelliSense. Browse them at
  https://lucide.dev/icons or on the dev host's Icon page.
- Icons are decorative (`aria-hidden`) unless you give them a `Label`.
- IzzyBlazor's own components use semantic roles (`<Icon Role="IconRole.Close" />`) resolved through
  `IconSet.Current`, so a different icon set can be swapped in without touching components.

## Buttons

- `Button` is a native `button`.
- `LinkButton` is an `a`, for navigation.
- `IconButton` shows only an icon. Its required `Label` becomes the accessible name and tooltip.

They share:

- `Accent` (`Primary`, `Neutral`, `Danger`) and `Variant` (`Solid`, `Soft`, `Outline`, `Ghost`, `Link`),
- `Size` (`ControlSize`, shared with form fields),
- `StartIcon`/`EndIcon`, `Loading`, `Disabled` and `FullWidth`.

On static pages buttons submit forms like plain HTML, including `Name`/`Value` to tell several submit
buttons apart. `OnClick` runs C# on interactive pages.

## Forms

`Form` wraps Blazor's `EditForm`, so one form works three ways: as a real POST on static pages (with
JavaScript off), with enhanced handling when scripts run, and with live validation on interactive
pages. Pair it with `[SupplyParameterFromForm(FormName = "...")]` on the model property; see
[Getting started](getting-started.md#forms-on-static-pages).

Fields (`TextField`, `TextArea`, `NumberInput`, `Select`, `Combobox`, `Checkbox`, `Switch`, `RadioGroup`):

- take their label, required marker, input type and length limits from the model's DataAnnotations,
- wire up `aria-describedby` and `aria-invalid`,
- show their own errors.

`Select` and `RadioGroup` list enum values automatically, using `[Display(Name)]`.

**NumberInput.** A native number input bound to any number type (`int`, `decimal`, `double`, and
their nullable forms), so it posts with the form, the browser checks `Min`, `Max` and `Step`, and phones
show a number keyboard. `Min` and `Max` default to the model's `[Range]`; `Step` defaults to 1 for
whole numbers and to any value for decimal types (set `Step="0.01"` for cents). Values always use
invariant formatting (`1.5`), as number inputs require, whatever the page's culture. With JavaScript,
stepper buttons replace the browser's spin buttons: hold one to repeat, and each end disables at
`Min` or `Max`. Arrow keys step either way. `Steppers="false"` leaves them out. It takes the same
`StartIcon` and add-ons as `TextField`.

**Add-ons.** `TextField` and `NumberInput` take `StartAddon` and `EndAddon`: content attached to the control, outside its
border, at the field's height. Use them for short text (`https://`, `kg`), a `Button` (a submit button
posts the form, so it works with JavaScript off), or another field, such as a currency `Select`, whose
label is kept for screen readers but not shown. Add-on text isn't part of the field's accessible name,
so put a unit that matters in the label too ("Weight (kg)"). Custom fields get the same look by
wrapping their `ib-field-control` in `FieldAddons`.

```razor
<TextField @bind-Value="Model.Website">
    <StartAddon>https://</StartAddon>
</TextField>
<TextField @bind-Value="Model.Price">
    <EndAddon><Select @bind-Value="Model.Currency" /></EndAddon>
</TextField>
```

**Select.** A native select. Where the browser supports the customizable select
(`appearance: base-select`, Chrome and Edge 135+), the open list is themed like a menu, and
`ItemTemplate` adds markup to options: an icon, a colored dot, or an `ib-option-hint` second line that
the closed field hides. Other browsers, including iOS Safari, keep their own list and show `ItemText`.

**Combobox.** A select you can type in.

- **No JavaScript:** it's a native select, so the form still posts.
- **With JavaScript:** typing filters the list, ignoring case and accents, and arrow keys and Enter
  choose. The hidden select keeps posting the value, so `@bind`, required validation and labels work
  unchanged.
- **Options:** give it `Items` (or an enum), or `Search` for long lists. `Search` is called with `""`
  for the first list, and as the user types on interactive pages (debounced, and cancelled when
  superseded).

**CheckboxGroup and MultiSelect.** Pick several options, bound to a collection (an array,
`List<T>` or `HashSet<T>`). Both post repeated form fields, which Blazor binds back to the collection,
so they work on static pages with no script. Validate the count with `[MinLength]`/`[MaxLength]`.
Both types are inferred from `@bind-Value` and `Items`; for an enum, pass
`Items="Enum.GetValues<Topping>()"`.

- **CheckboxGroup** shows every option as a checkbox, for short lists (`Orientation` for a row).
- **MultiSelect** suits longer lists. Without script it's a native multi-line select. With script it
  becomes a field of chips you can type in to filter: choosing toggles an option and the list stays
  open, Backspace removes the last chip, and each chip has a remove button.

## App layout

`AppShell` holds parts, each with its own settings:

- `AppHeader` (`Position`: sticky by default)
- `AppSidebar` (`Position` start or end, `FullHeight`, `Width`, `Breakpoint`)
- `AppMain`
- `AppAside` (`Width`, `Breakpoint`)
- `AppFooter` (`Position`)

Every part takes `Bg` and `Scheme`, and the sidebar and aside colors run the full height. `AppShell`
itself sets `ScrollMode` (`Page`, or `Main` for a window-filling frame where only the content
scrolls) and `Dividers`. The shell arranges itself from the parts present with CSS alone (`:has()`),
so it renders in one pass on static pages.

**The drawer.** Below its breakpoint the sidebar becomes a drawer opened by `SidebarToggle`. It works
with JavaScript off (a link to the sidebar and CSS `:target`); with JavaScript it also closes on
Escape, backdrop click and navigation.

**Sidebar header and footer.** `AppSidebar` takes `Header` and `Footer`, pinned to its top and bottom
(a workspace switcher, an account menu), with the content scrolling between them. They have no lines
at rest; a line shows next to one only while content is scrolled out of view there (CSS only).

Under a full-width `AppHeader`, the brand belongs in the header and the sidebar `Header` suits a
workspace switcher. With `FullHeight`, the sidebar `Header` is the top-left corner, so put the brand
there. Its row then matches `AppHeader`'s height, with a line that continues the header's.

**Collapsing to a rail.** `Collapsible` on `AppSidebar` shrinks it to an icon rail on wide screens.

- `SidebarToggle` collapses it there (and opens the drawer on phones). With no `SidebarToggle` in the
  shell, a chevron appears in the sidebar instead, at the bottom by default.
  `CollapseButtonPosition="VerticalPosition.Top"` moves it to the header row; `CollapseButton` forces
  it on or off.
- With JavaScript off it's a checkbox and CSS. With JavaScript the choice is kept in a cookie
  (`{Id}-collapsed`) that the server reads, so pages arrive already collapsed. On interactive pages
  `@bind-Collapsed` follows it and sets it from C#.
- A remembered choice wins over `Collapsed` when the page loads (`RememberState="false"` turns that off).
- In the rail, nav labels show as tooltips, badges and group items hide, opening a group expands the
  sidebar, and `Header`/`Footer` content stacks and centers. Mark text with the `ib-collapsed-hide`
  class (or `ib-collapsed-show` for rail-only content).

**Navigation.** `NavMenu` items work out their active state on the server. `NavGroup` is built on
`details` and opens for its `ActivePrefix` or when it holds the current page.

## Disclosure

`Accordion` with `AccordionItem`s, and `Collapsible` for a single "show more". Both are native
`details`, so they open and close with JavaScript off. `Exclusive` uses the shared `name` attribute to
keep one item open, and heights animate with no script where the browser supports it.

## Overlays

`Menu`, `Popover` and `Tooltip` use the Popover API and CSS anchor positioning. They open, close on
Escape and click-outside, and sit next to their trigger with no script.

`Dialog` is a native modal `dialog`, opened by `DialogTrigger`, by a link with `?dialog=id` (rendered
open on the server), or from C#. Close buttons submit a `method="dialog"` form, so closing works
without script too. Forms can't nest, so a dialog that has to sit inside a form takes `InsideForm`:
it then closes with the native `close` command instead. When a button in a dialog submits another
form or follows a link, the script closes the dialog, so it doesn't stay open over an enhanced
response.

`ConfirmButton` asks before acting: it opens a small alert dialog with a `Title` ("Delete this
project?"), an optional `Message` or `Body`, Cancel (focused first) and a confirm button
(`ConfirmText`, in the trigger's `Accent`). It takes the usual button look (`Accent`, `Variant`,
`Size`, icons). What confirming does:

- **Inside a form:** submits that form, with `Name=Value` if set, so one handler can tell which
  action it was. Works with JavaScript off.
- **`Form="id"`:** submits a form elsewhere on the page.
- **`Href`:** follows a link.
- **`OnConfirm`:** runs C# on an interactive page. On a static page it submits the form instead.

The backdrop doesn't close it; Escape and Cancel do.

Menu items are links, form posts or C# actions. The script adds menu arrow keys and type-ahead,
top-layer tooltips, and fallbacks for browsers missing anchor positioning or `commandfor`. Custom
triggers take the wiring attributes as context:
`<Trigger><IconButton ... @attributes="context" /></Trigger>`.

## Tabs

Each tab is a label around a radio button, and CSS shows its panel. With JavaScript off, tabs switch
instantly, arrow keys work, and form input in other panels is kept. With JavaScript the list becomes
proper ARIA tabs (roving focus, Home/End).

- `QueryKey="tab"` keeps the tab in the URL (`?tab=billing` opens Billing).
- `@bind-Selected` follows it on interactive pages.
- `Variant` (`Line`, `Soft`, `Segmented`), `Size` and `FullWidth`; each `Tab` takes a `Label`, `Icon`, `Badge` and `Disabled`.

## Breadcrumb and Pagination

Plain links. `Pagination` reads and writes `?page=` (page 1 leaves it out, and other parameters are
kept), so the page reads it with `[SupplyParameterFromQuery]` and paging works with no script. On
phones it shows only the neighbours and the ends. With `@bind-Page` on an interactive page, the pages
become buttons that update C# without touching the URL.

## Alerts and toasts

`Alert` takes a `Status`, `Variant` (`Soft`, `Outline`, `Solid`), `Title`, `Actions` and
`Dismissible`: a close button that works with JavaScript off, with `@bind-Dismissed` on interactive
pages. Danger and warning alerts are announced right away.

Toasts come from `ToastService` (`Toasts.Success("Saved")`) in any render mode:

- **A static form post** renders them in the response.
- **A redirect** carries them to the next page in a short-lived cookie.
- **Interactive pages** add them in the browser at once.

With JavaScript, toasts close after `Duration`, pausing on hover and focus; error toasts stay until
closed. Without JavaScript, they stay until closed.

A toast can carry one action, such as "View" or "Undo", and choosing it closes the toast. Toasts
with an action stay twice as long, so there's time to reach the button.

```csharp
Toasts.Info("Message archived.", action: new("Undo", Href: $"/inbox/restore/{id}")
{
    OnClick = () => Restore(id),   // interactive pages
});
```

`Href` makes the action a link, which works everywhere. `OnClick` runs C# on interactive pages; a
static page has no connection to run it over, so there the `Href` is used, and a toast with only
`OnClick` throws when shown on a static page.

## Loading: Spinner, Progress, Skeleton

All three are pure CSS, so they animate on static pages too. With reduced motion they slow down
rather than stop, since the motion is what says "still working".

- **`Spinner`** is a ring sized like an icon (`Size` takes `IconSize`), so it matches the text beside
  it. It's a `role="status"` region that screen readers hear as its `Label` ("Loading" by default).
- **`Progress`** is a bar with an optional `Label` above it (its accessible name). Set `Value` (0 to
  `Max`, 100 by default) and it reports the value to screen readers; `ShowValue` shows the
  percentage and `ValueText` replaces it ("3 of 5 files"). Leave `Value` out for an indeterminate bar
  that sweeps. `Color` and `Size` (thickness) are optional. On interactive pages, change `Value`
  from C#.
- **`Skeleton`** stands in for content still loading: `Lines` of text (the last of several is
  shorter), a block with `Height`, or a `Circle` (2.5rem, or `Width`). It takes the style props, and
  `Loaded` swaps in its `ChildContent`. It's hidden from screen readers, so announce the loading
  with a `Spinner` or the region's `aria-busy`. Table's loading rows use the same shimmer.

## Avatar

`Avatar` shows a photo (`Src`), else initials from `Name`, else an icon. The initials' color is
picked from the name, so a person looks the same on every page. The photo is drawn as a background
over the initials, so if it fails to load the initials show, in every browser and with no script.

- `Name` is also the accessible name. `Decorative` hides the avatar from screen readers when the
  name is written beside it; an avatar with no name is hidden from them anyway.
- `Size` (`Xs` to `Xl2`), `Square` for teams and companies, `Color` for a fixed fill, `Initials` to
  override the letters, `Icon` for the no-name icon.
- `Status` adds a dot in the corner (`Success` for online, say), with `StatusLabel` ("Online")
  joined to the accessible name.

`AvatarGroup` overlaps its avatars, each ringed in the page color (set `--ib-avatar-ring` on a
tinted surface). `Max` (1 to 10) shows the first few and counts the rest in a "+N" avatar, whose
accessible name is `MoreLabel` ("{0} more"). `Size` sets every avatar's size. Put the avatars
directly inside the group, since `Max` hides the extras by counting its children.

## Card

`Title`, `Description`, `Actions`, `Media`, content and `Footer`, in `Outline`, `Elevated` or
`Filled`. `Href` makes the whole card a link through its title (one link for screen readers), while
buttons in it stay clickable.

## Table

Sorts and pages through the URL: headers and page numbers are links that set `?sort=` (`-total` for
descending) and `?page=`. So tables work with no script, survive reloads and can be shared. On
interactive pages the same links navigate without a reload, and the table follows.

- `PropertyColumn` takes its title from the property (or `[Display(Name)]`), right-aligns numbers and
  formats values (`Format="C"`).
- `TemplateColumn` shows anything and sorts with `SortBy`.
- Pass `Items` (a list, or an EF Core `IQueryable`, sorted and paged by the database) or an
  `ItemsProvider` that loads each page (`request.Apply(query)`).
- Also: `PageSize`, `InitialSort`, `QueryPrefix` for several tables on a page, `MaxHeight` for a
  sticky header, `Striped`, `Size`, `RowClass`, `EmptyText`.

**Selection.** `Selectable` adds a checkbox to each row, valued with the row's `ItemKey`.

- **No JavaScript:** inside a form, the checked rows post their keys under `SelectionName`
  (`"selected"` by default); read them with `[SupplyParameterFromForm]`. Checked rows are tinted.
- **With JavaScript:** a header checkbox selects every row on the page, and shows when only some are.
- **Interactive pages:** `@bind-SelectedKeys` follows the checkboxes.

**Row links.** `RowHref` makes each row a link (to the item's page, say). The whole row is
clickable, while links, buttons and checkboxes inside it still work on their own; `RowLabel` names
the row for screen readers.

**Footer row.** Give columns `FooterText` (or `Footer` for markup) for totals.

**Loading.** `Loading` dims the rows, or shows placeholder rows when there are none yet. It's set
automatically while an `ItemsProvider` loads on interactive pages.

## Filtering

Tables don't filter themselves (built-in column filters belong to a future data grid); the page
filters the data it passes in. `FilterForm` makes that easy: a search-and-filter bar whose values go
in the URL (`?q=ada&status=Paid`), read with `[SupplyParameterFromQuery]`.

- **No JavaScript:** a GET form that keeps the rest of the query string (the table's sort) and
  starts over at page 1.
- **With JavaScript:** it applies as you type and on every change, without a reload, keeping focus.
- **Interactive pages:** it navigates through Blazor.

Fields: `FilterSearch` (`?q=`), `FilterSelect` (`Values` or an enum) and `FilterCheckbox`. List your
own inputs' names in `Fields`.

## Open state from C#

Disclosures and overlays take `@bind-Open`. The browser always does the opening and closing; on
interactive pages an `ibtoggle` event reports changes back, and setting `Open` from C# opens or closes
the element.
