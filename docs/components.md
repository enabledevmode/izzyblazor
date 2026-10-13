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
- [Banner](#banner)
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

`Anchor` is a text link, underlined by default (`Underline` changes it). `Inherit` takes the text color, and
`External` opens a new tab with an icon and a note for screen readers. (It's `Anchor`, not `Link`: Razor treats `<Link>` as the HTML `<link>` element, which can't have content.)

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

**ButtonGroup.** Related buttons joined into one control, with `role="group"` and a `Label` for
screen readers. Its `Accent`, `Variant` and `Size` become the defaults for the buttons inside; a
button's own setting wins, so one button can be `Danger` in a neutral group. `Attached="false"`
spaces the buttons apart, `Orientation` stacks them, and `FullWidth` shares the width among them.
A wrapper holding a button (a `Menu`'s trigger) joins the group too, but `Menu` sets its trigger's
look itself, so give it the group's settings.

```razor
<ButtonGroup Label="Calendar view" Variant="ButtonVariant.Outline" Accent="Accent.Neutral">
    <Button>Day</Button>
    <Button>Week</Button>
    <Button>Month</Button>
</ButtonGroup>
```

## Forms

`Form` wraps Blazor's `EditForm`, so one form works three ways: as a real POST on static pages (with
JavaScript off), with enhanced handling when scripts run, and with live validation on interactive
pages. Pair it with `[SupplyParameterFromForm(FormName = "...")]` on the model property; see
[Getting started](getting-started.md#forms-on-static-pages).

Fields (`TextField`, `TextArea`, `NumberInput`, `Slider`, `Select`, `Combobox`, `Checkbox`, `Switch`, `RadioGroup`):

- take their label, required marker, input type and length limits from the model's DataAnnotations,
- wire up `aria-describedby` and `aria-invalid`,
- show their own errors.

`Select` and `RadioGroup` list enum values automatically, using `[Display(Name)]`.

**NumberInput.** A native number input bound to any number type (`int`, `decimal`, `double`, and
their nullable forms), so it posts with the form, the browser checks `Min`, `Max` and `Step`, and phones
show a number keyboard. `Min` and `Max` default to the model's `[Range]`; `Step` defaults to 1 for
whole numbers and to any value for decimal types (set `Step="0.01"` for cents). Values always use
invariant formatting (`1.5`), as number inputs require, whatever the page's culture. With JavaScript,
stepper buttons appear: hold one to repeat, and each end disables at `Min` or `Max`.
`StepButtons` places them: `StepButtons.Stacked` at the end (the default), `Split` (a full-height
button at each end, easier to tap) or `None`. The browser's own spin buttons are always hidden, and
arrow keys step either way. It takes the same
`StartIcon` and add-ons as `TextField`.

**Slider.** A native range input bound to a number type, so it posts with the form and takes arrow
keys, Page Up/Down and Home/End with no script. `Min` and `Max` default to `[Range]`, or 0 to 100;
`Step` to 1. With JavaScript the track fills up to the thumb and the value shown by `ShowValue`
follows as you drag. `ValueFormat` sets how the value reads, both shown and announced to screen
readers: `"{0}%"`, `"${0}"`. On interactive pages the bound value updates on release; `Live` updates
it while dragging, at one round trip per move. A read-only slider is disabled and posts its value
through a hidden input.

**FileUpload.** A drop zone around a real file input, named by `Name`.

- **No JavaScript:** the input's button chooses files and they post with the form. Set `Multipart`
  on the `Form` (it sets `enctype="multipart/form-data"`; without it only file names post), and
  bind the files on the page with `[SupplyParameterFromForm(FormName = "...")]`: `IFormFile` for one,
  `IFormFileCollection` for `Multiple`, named like the field.
- **With JavaScript:** files can be dropped anywhere on the zone. Dropped files that don't match
  `Accept` are refused, and a picked file of the wrong type (the picker's "All files" lets one
  through) or over `MaxFileSize` is flagged before the form posts. A form that isn't multipart logs a console warning.
- **Interactive pages:** `OnChange` receives the chosen files as `IBrowserFile`, up to `MaxFiles`
  (10 by default) and leaving out any over `MaxFileSize`. Read one with
  `OpenReadStream(maxAllowedSize)`: its default limit is 500 KB. Nothing posts.

`Accept` filters the picker (`"image/*"`, `".pdf"`), but always check types and sizes on the server,
and raise the server's request size limit to match `MaxFileSize`. `Errors` shows server-side
messages under the zone.

```razor
<Form Model="Model" FormName="profile" Multipart OnValidSubmit="SaveAsync">
    <FileUpload Name="Avatar" Label="Profile photo" Accept="image/*" MaxFileSize="2_000_000" />
</Form>

@code {
    [SupplyParameterFromForm(FormName = "profile")] public IFormFile? Avatar { get; set; }
}
```

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

**Checkbox, RadioGroup and Switch.** Checkboxes and radios are drawn with the field colors, so
they match fields and switches in every browser and in dark mode, and they follow `Size`. In
Windows high-contrast mode they fall back to the browser's own boxes. `Checkbox Indeterminate`
shows a dash, for a "select all" box when only some items are selected. It's display only: the box
posts as unchecked and choosing it clears the dash. CSS shows it with no script; with script,
screen readers announce it as "mixed". Work it out from the other values, so it's right after a
post too.

**SegmentedControl.** One choice from a few (a view, an alignment, a billing period), as joined
buttons. They're real radio buttons drawn as segments, so it posts like a `RadioGroup`, arrow keys
move the choice, and it works with no script. It lists enum values automatically, or takes `Items`
and `ItemText`. `ItemIcon` adds an icon per option and `IconOnly` shows only the icons (the text
stays for screen readers and as a tooltip). `FullWidth` shares the width equally; `HideLabel` keeps
the label for screen readers only, as in a toolbar.

**CheckboxGroup and MultiSelect.** Pick several options, bound to a collection (an array,
`List<T>` or `HashSet<T>`). Both post repeated form fields, which Blazor binds back to the collection,
so they work on static pages with no script. Validate the count with `[MinLength]`/`[MaxLength]`.
Both types are inferred from `@bind-Value` and `Items`; for an enum, pass
`Items="Enum.GetValues<Topping>()"`.

- **CheckboxGroup** shows every option as a checkbox, for short lists (`Orientation` for a row).
- **MultiSelect** suits longer lists. Without script it's a native multi-line select. With script it
  becomes a field of chips you can type in to filter: choosing toggles an option and the list stays
  open, Backspace removes the last chip, and each chip has a remove button.

**What a static form post binds.** Blazor sets every model field that isn't in the post to its
type's default: `false`, `0`, and `null` for strings and collections, not the property's initial
value. And when nothing of a model is posted, it doesn't bind the model at all, so `Model ??= new()`
recreates it with its initial values. In practice:

- `Checkbox` and `Switch` post a hidden `false` after the box, so an unchecked box always binds
  `false`, even when it's the only field in the form.
- An empty `CheckboxGroup` or `MultiSelect` posts nothing, so its collection binds as `null`. The
  components show that as nothing chosen; in your own code use `?.` or `?? []`. If the form has
  nothing else to post, the model is recreated and a default selection comes back. So set default
  selections when the page first loads, not in the property initializer.
- Disabled fields aren't posted, so they bind their defaults too. For a text field that should
  post but not change, use `ReadOnly`; otherwise keep the value out of the form and set it on the
  server.

## App layout

`AppShell` holds parts, each with its own settings:

- `AppHeader` (`Position`: sticky by default)
- `AppSidebar` (`Position` start or end, `FullHeight`, `Width`, `Breakpoint`)
- `AppMain`
- `AppAside` (`Width`, `Breakpoint`)
- `AppFooter` (`Position`)

Every part takes `Bg` and `Scheme`, and the sidebar and aside colors run the full height. `AppShell`
itself sets `ScrollMode` (`Page`, or `Main` for a window-filling frame where only the content
scrolls), `Dividers`, and `Variant`, how the parts are layered. Content is the brightest layer and
the frame recedes into the page tint: `Tinted` (the default) tints the sidebar, `Inset` tints the
whole frame and raises the main area as a rounded panel, `Flat` is all surface with lines, and
`Canvas` tints the main area for dashboards whose content is all cards. A part's `Bg` still wins. The shell arranges itself from the parts present with CSS alone (`:has()`),
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
response. A form inside the dialog is different when the response comes back with validation errors: the
script reopens the dialog and focuses the first field with an error. On interactive pages the
script leaves the dialog open after a submit; close it from your submit handler (with
`@bind-Open`) when the save succeeds. With JavaScript off, a dialog
opened with `?dialog=id` posts to the same address, so the server renders it open again.

`Drawer` is a `Dialog` that slides in from an edge: the end side by default, or `Placement` `Start`,
`Top` or `Bottom` (a sheet along the bottom suits phones). It opens and closes the same ways and
traps focus the same way. `Size` sets a side drawer's width (`Sm` 20rem, `Md` 24rem, `Lg` 32rem,
`Xl` 40rem, `Full`); top and bottom sheets are as tall as their content, up to 85% of the screen.
Sides flip in right-to-left languages.

**Dialogs from code (interactive pages).** `DialogService` opens a component in a dialog and awaits
how it ends, for flows like "edit this, then refresh":

```razor
@inject DialogService Dialogs

var result = await Dialogs.OpenAsync<EditSite>("Edit site", new() { ["Site"] = site });
if (!result.Cancelled) await ReloadAsync();

if (await Dialogs.ConfirmAsync("Ada loses access.", "Remove member?",
        new() { ConfirmText = "Remove", Accent = Accent.Danger })) { ... }
```

The component gets a cascading `DialogInstance` to close with data (`Dialog.Close(site)`) or cancel;
Escape, the backdrop and the close button cancel too (`result.Cancelled`). Put its buttons in
`DialogActions`, last, for a footer row. `DialogOptions` set the size, placement (a drawer or sheet)
and closing behavior. It needs one `<DialogHost />` on the interactive page or in an interactive
layout (`@rendermode` on the host where the layout is static). Static pages can't open anything from
code, so calling it there throws; use `Dialog` with `DialogTrigger` or a `?dialog=` link, or
`ConfirmButton`, which work with JavaScript off.

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

Menu items are links, form posts or C# actions. Menu roles (`menu`, `menuitem`) promise arrow keys,
so they come with the script: with JavaScript off the items are plain links and buttons, a checked
one marked pressed or current. A `Popover` opened by the user takes focus (its `[autofocus]`
element, or the panel), and closing returns focus to the trigger.

The script adds menu arrow keys and type-ahead,
top-layer tooltips, and fallbacks for browsers missing anchor positioning or `commandfor`. Custom
triggers take the wiring attributes as context:
`<Trigger><IconButton ... @attributes="context" /></Trigger>`.

**Checkable menu items.** `Checked` makes a `MenuItem` a checkbox item that shows a check, with
`@bind-Checked` on interactive pages. On static pages its action is still a link or a form post (a
link that toggles `?compact=1`, say), and the server decides what's checked. For pick-one options,
put `MenuItem`s with a `Value` in a `MenuRadioGroup`:

- `QueryKey="sort"`: each option is a link that sets `?sort=value`, keeping the rest of the URL, and
  the checked one comes from the URL. Works with JavaScript off.
- `Name="sort"`: each option is a submit button that posts `sort=value` with its form.
- `@bind-Value`: choosing updates C# on interactive pages.

**Submenus.** `MenuSub` is an item that opens a second menu beside it, as a nested popover: clicking
it opens it with no script. With script it also opens on hover and with the arrow key toward it,
and the arrow key back or Escape returns to its item. Choosing any item closes the whole menu.

```razor
<Menu Label="View">
    <MenuRadioGroup Label="Sort by" QueryKey="sort">
        <MenuItem Value="name">Name</MenuItem>
        <MenuItem Value="date">Date modified</MenuItem>
    </MenuRadioGroup>
    <MenuDivider />
    <MenuSub Label="Move to" Icon="Lucide.FolderInput">
        <MenuItem>Inbox</MenuItem>
    </MenuSub>
</Menu>
```

## Tabs

Each tab is a label around a radio button, and CSS shows its panel. With JavaScript off, tabs switch
instantly, arrow keys work, and form input in other panels is kept. With JavaScript the list becomes
proper ARIA tabs (roving focus, Home/End).

- `QueryKey="tab"` keeps the tab in the URL (`?tab=billing` opens Billing).
- `@bind-Selected` follows it on interactive pages.
- `Variant` (`Line`, `Soft`, `Segmented`), `Size` and `FullWidth`; each `Tab` takes a `Label`, `Icon`, `Badge` and `Disabled`.
- `Orientation="Orientation.Vertical"` puts the tabs in a column beside the panels from 640px up,
  and Up and Down arrows move between them. On phones they stay a scrolling row above the panels.

**Tabs as links.** For sections split across routes (`/settings/profile`, `/settings/billing`), use
`TabLink` children instead of `Tab`. The `Tabs` then renders a `<nav>` of real links with no panels
and no script, and the tab for the current page gets `aria-current` from the URL on the server
(`Match` works as on `NavItem`). Variants, sizes, `FullWidth` and `Orientation` apply the same way.
A `Tabs` holds either `Tab`s or `TabLink`s, not both.

```razor
<Tabs Label="Settings">
    <TabLink Href="settings/profile" Label="Profile" Icon="Lucide.User" />
    <TabLink Href="settings/billing" Label="Billing" />
</Tabs>
```

## Breadcrumb and Pagination

Plain links. `Pagination` reads and writes `?page=` (page 1 leaves it out, and other parameters are
kept), so the page reads it with `[SupplyParameterFromQuery]` and paging works with no script. On
phones it shows only the neighbours and the ends. With `@bind-Page` on an interactive page, the pages
become buttons that update C# without touching the URL.

## Stepper

Where someone is in a process, such as a checkout: `Stepper Current="2"` (counting from 1) around
`Step`s with a `Label`, optional `Description` and `Icon`. Steps before `Current` show a check, and
screen readers hear `CompletedLabel` ("(completed)") after their label; the current step has
`aria-current="step"`. A `Step` with `Href` is a link, to go back to a finished step, say. It's a row
from 640px up and a column on phones (`Orientation.Vertical` for a column everywhere); `Size` scales
the markers. Nothing needs script: drive `Current` from the URL or the form post. For a step that only sometimes applies, set `Hidden` rather than wrapping it in `@if`: a step Blazor adds later would otherwise be numbered after the rest.

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

Two cases lose a toast, both rare:

- **An interactive page whose connection drops** (Blazor Server) loses toasts that hadn't been shown
  yet; the page reconnects without them.
- **A redirect after the response has started streaming** (a page with `[StreamRendering]` that
  redirects partway through) can't set the cookie that carries toasts, since the headers are already
  sent. Redirect before streaming starts, or show the message on the page.

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

## Banner

A full-width strip for a message about the whole app: maintenance, a trial that's ending, a license
warning. Use `Alert` for a message about part of a page.

```razor
<AppShell>
    <Banner Status="Status.Warning" Dismissible RememberKey="maintenance-2026-11">
        <ChildContent>Scheduled maintenance on Sunday from 02:00 to 04:00 UTC.</ChildContent>
        <Actions><Anchor Href="/status">Details</Anchor></Actions>
    </Banner>
    <AppHeader>...</AppHeader>
    ...
</AppShell>
```

- **Where it goes:** at the top of the page, or inside `AppShell`, where it takes a row of its own
  above the header across every column (a full-height sidebar starts below it). By default it
  scrolls away with the page and a sticky header stays; `Position="BarPosition.Sticky"` keeps it at
  the top, with the sticky header and sidebar below it. The shell makes room for one line (37px)
  with no script; with script it measures the banner when the message wraps.
- **Look:** `Status` (Info by default) and `Variant` (`Soft` by default, `Outline`, `Solid`), as on
  Alert. `Color` replaces the status's color, for a brand message such as `Color.Primary`; the status
  still sets the icon and what screen readers hear. `Icon` and `HideIcon` change the icon, `Title`
  adds a bold lead-in, and `Actions` holds a link or small button after the message.
- **Dismissing:** `Dismissible` adds a close button that works with JavaScript off, with
  `@bind-Dismissed` on interactive pages.
- **Staying dismissed:** with `RememberKey`, closing it writes a cookie (`ib-banner-{key}`, for a
  year). The server reads it and leaves the banner out of later pages, static or interactive; change
  the key to show a new message to everyone. Reading the cookie needs `AddIzzyBlazor()` on the
  server. In WebAssembly the banner renders, and the script closes it as soon as it appears. With
  JavaScript off the close button works, but nothing is remembered. Keys are letters, digits, `-`,
  `_` and `.`.
- **Accessibility:** it's a region named "Announcement" (`Label`), so screen reader users can find
  it. It isn't announced on its own, since it's there when the page loads; set `Announce` for a
  banner you show in response to something, such as a lost connection.

## Tag

A keyword, category or applied filter: larger than a `Badge`, with the same `Status`, `Color` and
variants (`Soft`, `Solid`, `Outline`), and `Size` (20, 24 or 28px). `Label` is the text (or content),
`StartIcon` an icon, and `Href` makes the text a link. To make it removable, give it `RemoveHref` (a
link, such as the current URL without that filter, for static pages) and/or `OnRemove` (C#, used on
interactive pages). The remove control is named "Remove {Label}" (`RemoveLabel`).

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

## Stat

A key figure: `Label`, `Value` (or content), and optionally `Change` with a `Trend` (`Up`, `Down`,
`Flat`) and a muted `Description` such as "vs last month". It's a `<dl>`, so the label names the
value for screen readers. Up is shown in the success color and down in danger; set `ChangeStatus`
when down is good, such as for costs or response times. The arrow is hidden from screen readers and
`TrendLabel` ("Up", "Down", "No change") is read in its place. `Icon` adds a tinted square at the
end; `Size` scales the text. Put stats in a `Grid`, in `Card`s or not.

## DescriptionList

Terms and their details, such as an order's fields: `DescriptionItem Term="Status"` (or
`TermContent`) with the detail as content, rendered as a `<dl>` with each pair in a `<div>`.
`Orientation.Horizontal` (the default) puts terms beside details from 640px up, the term column as
wide as the longest term (up to 40%); phones get them stacked. `Orientation.Vertical` puts each term
above its detail, and `Columns` (1 to 4) flows the items into columns from 640px up. `Divided` adds
lines between items; `Size` sets the text size and spacing.

## EmptyState

What to show when there's nothing yet: `Icon` (in a tinted circle), `Title` (`TitleLevel`, default
3), `Description`, any content, and `Actions` such as a "New invoice" button or "Clear filters".
`Bordered` draws a dashed outline for an empty area inside a page; `Size` scales it.

## Kbd

A key, `<Kbd>Esc</Kbd>`, or a combination, `<Kbd Keys="Ctrl+K" />`: nested `<kbd>` elements, one
box per key, joined by `Separator` ("+"). Spaces around "+" are ignored, and "Ctrl++" is Ctrl and the
plus key. Sized in em, so it fits the text around it.

## Table

Sorts and pages through the URL: headers and page numbers are links that set `?sort=` (`-total` for
descending) and `?page=`. So tables work with no script, survive reloads and can be shared. On
interactive pages the links are ordinary navigations and the table follows the URL. With enhanced
navigation that's no full reload, but the server renders the page again; only an app with a
client-side router (full interactivity) stays in the browser.

**Column filters.** `Filterable` on a column adds a filter. Where it goes is `FilterPlacement`:

- **`Header` (the default):** a funnel button in the column's header, tinted while the column is
  filtered. It opens the filter in a popover (a menu of values for enums and booleans), and the
  active filters show as removable tags above the table, with Clear. Popovers open natively, so
  this works with JavaScript off: Enter or Apply in the popover submits.
- **`Row`:** a row of fields under the headers, always in view, for tables people filter
  constantly. With JavaScript off, Enter or the Apply button below the table submits.

What each column gets:

- **Text** (a `string` property) matches anywhere, ignoring case. **Numbers** match exactly. **Enums
  and booleans** get a select with an "All" option.
- **Dates** (`DateOnly`, `DateTime`, `DateTimeOffset`) get a date input and match that day:
  `?f-date=2026-10-13`. A `DateTime` keeps the day's range, so a database index still helps; a
  `DateTimeOffset` matches the day on its own clock. Date ranges belong to a future data grid.
- **Several values:** `FilterMultiple` on an enum column makes its menu checkboxes that stay open
  while you tick (in the row, behind a field showing "All", the value or "2 selected"). It keeps
  the items with any chosen value (`IN (...)` in SQL). The URL repeats the key
  (`?f-status=Paid&f-status=Pending`); with JavaScript off each item is a link, so the page
  reloads per tick. Bound, `Filters` holds them joined: `"Paid,Pending"`. `MenuItem` has the same
  `KeepOpen` for your own menus.
- A `TemplateColumn` filters by `FilterBy` (text), and needs a `SortKey` to name its filter.
- Other types throw; use a `TemplateColumn` with `FilterBy`, or a `FilterForm`.
- Filters go in the URL as `?f-{key}=` (`?f-customer=ada`), with the table's `QueryPrefix`. With no
  script they submit a GET form; with script they apply as you type. A new filter starts at page 1.
- The filters are expressions on the column's property, so an EF Core `IQueryable` filters in the
  database. An `ItemsProvider` gets them as `request.Filters`; `request.Apply(query)` filters,
  sorts and pages, and `request.ApplyFiltering(query)` is what to count.
- A filterable table can sit inside a form (a selection form): the filters never post with it.

**State in the component.** To sort, page and filter in place on an interactive page, like a
client-side grid, bind `Sort`, `Page` or `Filters`:

```razor
<Table Items="orders" PageSize="50" @bind-Sort="sort" @bind-Page="page" @bind-Filters="filters">...</Table>
```

Headers and page numbers become buttons, filters apply after a pause in typing, and the URL doesn't
change. The table starts from the URL's `?sort=`, `?page=` and filters if they're there, and a new
sort or filter goes back to page 1. `Filters` is a dictionary of column key to value. Set
`page = 1` yourself when your own filters change. Static pages, and the prerender before the page
is interactive, still use the URL.

- `PropertyColumn` takes its title from the property (or `[Display(Name)]`), right-aligns numbers and
  formats values (`Format="C"`).
- `TemplateColumn` shows anything and sorts with `SortBy`.
- Pass `Items` (a list, or an EF Core `IQueryable`, sorted and paged by the database) or an
  `ItemsProvider` that loads each page (`request.Apply(query)`).
- Also: `PageSize`, `InitialSort`, `QueryPrefix` for several tables on a page, `MaxHeight` for a
  sticky header, `Striped`, `Size`, `RowClass`, `EmptyText`.
- Page size: `PageSizeOptions="new[] { 10, 25, 50 }"` adds a "Rows per page" menu to the footer.
  Each size is a link (`?size=25`, only listed sizes are accepted from the URL), so it works with
  no script, and the new page is the one holding the first row you were looking at.
  `@bind-PageSize` (an `int?`) keeps it in the component on interactive pages.
- Lines: `RowLines` (on by default), `ColumnLines` (off by default) and `Bordered`, the border
  around the table (on by default; turn it off for a table inside a card).

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

For a filter per column, use the Table's `Filterable` columns (above). For a search box or filters
that aren't columns, the page filters the data it passes in, and `FilterForm` makes that easy: a
search-and-filter bar whose values go in the URL (`?q=ada&status=Paid`), read with
`[SupplyParameterFromQuery]`.

- **No JavaScript:** a GET form that keeps the rest of the query string (the table's sort) and
  starts over at page 1.
- **With JavaScript:** it applies as you type and on every change, without a reload, keeping focus.
- **Interactive pages:** it navigates through Blazor.

Fields: `FilterSearch` (`?q=`), `FilterSelect` (`Values` or an enum) and `FilterCheckbox`. List your
own inputs' names in `Fields`.

## ColorSchemeSwitch

Lets the user pick System, Light or Dark: three segments with icons and labels, or `IconOnly` (the
names stay for screen readers and as tooltips), in `Size` small, medium or large.

```razor
<ColorSchemeSwitch />
<ColorSchemeSwitch IconOnly Size="ControlSize.Sm" />
<ColorSchemeSwitch @bind-Value="scheme" />   @* interactive pages *@
```

The choice is kept in the `ib-scheme` cookie. Put `IzzyTheme` in `<head>` so the server applies it
before the first paint. With JavaScript the switch is instant and other open tabs follow; with it
off, each option is a link to `/_izzy/scheme`, which `app.MapIzzyBlazor()` adds: it saves the
cookie and redirects back to the page.

## Scrollbars

IzzyBlazor's scrolling areas (the sidebar, tables, menus, dialogs, code blocks and so on) have thin,
theme-colored scrollbars with no arrow buttons, a little stronger while the pointer is on the
thumb. `--ib-color-scrollbar` and `--ib-color-scrollbar-hover` set the colors; high-contrast mode
uses system colors.
The page's scrollbar gets the same look when the page uses `AppShell`; on other
pages add `ib-scrollbars` to `<html>`. For a scroll region of your own (a chat log, a long list in a
card), add `ib-scroll` to the element that scrolls. On macOS these stay visible rather than auto-hiding as the system's do.

## Text and other languages

Components name their buttons and announce states in English by default: "Close", "Loading",
"Next page", "No matches". Each of these is a parameter on the component (`CloseLabel`,
`LoadingLabel`, `EmptyText`...), and all of them can be set app-wide by registering an `IzzyText`:

```csharp
// One language: change the strings you need.
builder.Services.AddSingleton(new IzzyText { Close = "Schließen", Loading = "Wird geladen" });

// Several: subclass it and read your resources for the current UI culture.
public sealed class AppText(IStringLocalizer<AppText> l) : IzzyText
{
    public override string Close => l["Close"];
    public override string NextPage => l["Next page"];
}
builder.Services.AddSingleton<IzzyText, AppText>();
```

Properties are read on every render, so a per-request culture works. A component's own parameter
wins over `IzzyText`. Strings with `{0}` (such as `MoreAvatars`, "{0} more") are format strings.
Validation messages come from your model's DataAnnotations, not from here.

## Open state from C#

Disclosures and overlays take `@bind-Open`. The browser always does the opening and closing; on
interactive pages an `ibtoggle` event reports changes back, and setting `Open` from C# opens or closes
the element.
