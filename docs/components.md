# Components

What each component does, and how it behaves with JavaScript off, with JavaScript, and on
interactive pages. Every component has a live demo in the dev host, in both static and interactive
modes (see [Contributing](../CONTRIBUTING.md)).

Most components take the item props, which place them in a layout (`Margin`, `Width`, `Grow`,
`AlignSelf` and the rest), and boxes also take the surface props (`Padding`, `Bg`, borders and the
rest). [Styling](styling.md#which-components-take-which-props) lists which take which.

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
- [Stepper](#stepper)
- [Alerts and toasts](#alerts-and-toasts)
- [Banner](#banner)
- [Badge](#badge)
- [Tag](#tag)
- [Loading: Spinner, Progress, Skeleton](#loading-spinner-progress-skeleton)
- [Avatar](#avatar)
- [Card](#card)
- [Stat](#stat)
- [DescriptionList](#descriptionlist)
- [EmptyState](#emptystate)
- [Image and VideoEmbed](#image-and-videoembed)
- [Timeline](#timeline)
- [Kbd](#kbd)
- [Table](#table)
- [Filtering](#filtering)
- [ColorSchemeSwitch](#colorschemeswitch)
- [Scrollbars](#scrollbars)
- [Text and other languages](#text-and-other-languages)
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

`GridItem` spans rows (`RowSpan`); any component spans columns with `ColSpan`. For content-sized items that flow, use `Stack` or `Row` with `Wrap`.

The rest:

- `Container` centers content at a readable width (`Sm` to `Xl`, or `Full`).
- `Section` is a band of a page (a hero, a feature row, a newsletter sign-up): a `section` with
  vertical padding (`Spacing` `Sm`, `Md` by default, `Lg`, or `None`) and any style prop, such as
  `Bg`. Inside a main area with a `ContentWidth` it spans the whole main area (full bleed) while its
  content stays on the column; `FullBleed="false"` keeps it on the column. It measures the main area
  with CSS alone, so it works with no script; put it at the page's top level or in containers
  without side padding of their own.
- `Center` centers both ways.
- `Spacer` pushes the rest of a row to the end.
- `AspectRatio` keeps media at a shape (16:9 by default).
- `Divider` draws a line: plain, dashed, labeled ("or") or vertical.

## Typography, links and code

`TextBlock` (body text, a `p` by default) and `Heading` (`Level` 1 to 6) take every style prop plus:

- `Size`, `Weight`, `Family`, `Align`, `LineHeight`, `LetterSpacing`,
- `Transform`, `Decoration`, `Italic`,
- `Wrap` (including `TextWrap.Balance` and `Pretty`), `Truncate`, `LineClamp`, `TabularNums`.

`Size` and `Align` are responsive. `MinSize` makes the size fluid instead: the text is `MinSize` on a
360px screen and grows smoothly to `Size` at 1280px, as for a hero heading
(`<Heading Level="1" Size="TextSize.Xl5" MinSize="TextSize.Xl3">`). Text components have no default margins; space them with a
Stack's `Gap`. (It's `TextBlock`, not `Text`, because Razor reserves `<text>` in code blocks.)
On a page with an `OnThisPage`, headings in the main area get ids and link to themselves (see
[On this page](#app-layout)); `Outline` and `Anchor` on `Heading` change that.

`List` is a bulleted list of `ListItem`s; `Ordered` numbers it, `Plain` drops the bullets and indent,
and `Icon` puts an icon in place of each bullet (`<List Icon="Lucide.Check" IconColor="Color.SuccessText">`
for a check list; an item's own `Icon` wins). `Gap` spaces the items (8px by default). Lists without
bullets keep `role="list"`, which Safari drops otherwise.

`Prose` styles plain HTML inside it, such as Markdown output or a CMS field
(`<Prose>@((MarkupString)html)</Prose>`): headings, paragraphs, links, lists, quotes, inline code and
code blocks, tables, images, rules and `kbd`, in the library's type, with spacing on the rem scale and
whole-pixel line heights. `Size` is `Sm`, `Md` (default) or `Lg`. Its rules weigh no more than a class,
so library components inside keep their own look and your classes win. Only render HTML you trust or
have sanitized.

`Anchor` is a text link, underlined by default (`Underline` changes it). `Inherit` takes the text color, and
`External` opens a new tab with an icon and a note for screen readers; `HideExternalIcon` drops the
icon (for repeated links such as a list of lessons) and keeps the note. (It's `Anchor`, not `Link`: Razor treats `<Link>` as the HTML `<link>` element, which can't have content.) In running text an `Anchor` is
inline, so of the layout props only side margins apply; widths and top and bottom margins work where a
`Row`, `Stack` or `Grid` lays it out.

`Code` is inline, or a `Block` that scrolls sideways with an optional `Language` label. Its copy
button appears only when JavaScript runs (`Copyable="false"` leaves it out). `MaxHeight` caps a
block's height, such as `Length.Rem(20)` for a long file: the code scrolls and the label and copy
button stay in place.

A block's `Title` (a file name, such as `Program.cs`) goes in a header row above the code, in place
of the language label. `LineNumbers` numbers the lines, and `HighlightLines` marks some with a tinted
background, by number from 1: `HighlightLines="3-5,9"` (anything else throws). Both are drawn on the
server: each line is its own inline element (a `mark` when highlighted), the tint is the block's
background, and the numbers are generated by CSS, so copying or selecting still gives the code as
written. They need the code as plain text, as
highlighting does.

```razor
<Code Block Language="csharp" Title="Program.cs" LineNumbers HighlightLines="4,7-8">@program</Code>
```

`CodeExample` shows a live example with its source under it, in one frame: the content is the
preview, and `Code` (with `Language`, Razor by default, `Title`, `HighlightLines`, `LineNumbers`
and `MaxHeight`) is the source, shown as a `Code` block with its copy button. Several files go in
`CodeExampleFile` parts (`Title`, `Code`, `Language`, `HighlightLines`) written among the content,
shown as tabs over the source that switch with no script. `Collapsible` hides the source behind
"Show code" (a `details`, so it works with JavaScript off; `SourceOpen` starts it shown), and
`Center` centers the preview. `--ib-example-padding` and `--ib-example-bg` set the preview's padding
and background.

```razor
<CodeExample Center Collapsible>
    <Button StartIcon="Lucide.Save">Save</Button>
    <CodeExampleFile Title="Buttons.razor" Code="@markup" />
    <CodeExampleFile Title="Buttons.razor.cs" Language="csharp" Code="@codeBehind" />
</CodeExample>
```

`CopyButton` copies other text: `Text`, or the value or text of the element whose id is `For` (an
install command, an ID, an API key in a read-only field). It's a ghost neutral icon button named
"Copy" (`Label`) unless set otherwise, takes the buttons' `Accent`, `Variant`, `Size` and
`Disabled`, and with content shows that text after the icon. After copying it shows a check and
announces `CopiedLabel` for a moment. Copying needs JavaScript, so like a block's copy button it
appears only when the script runs. In a field's `EndAddon` it's drawn as the add-on's button, and
with JavaScript off the add-on is left out. It draws its own icons, so `StartIcon`, `EndIcon` and
`Loading` aren't accepted.

```razor
<Code>dotnet add package IzzyBlazor</Code>
<CopyButton Text="dotnet add package IzzyBlazor" Label="Copy the command" />
```

**Syntax highlighting.** When the `Language` is one the built-in highlighter knows, the code is colored
on the server, so it works with JavaScript off and never flashes plain first. It knows C# (`csharp`,
`cs`), Razor (`razor`), HTML and XML (`html`, `xml`, `svg`), CSS (`css`), JavaScript and TypeScript (`js`,
`ts`), JSON (`json`) and shell (`bash`, `sh`, `powershell`, `console`). Any other name, such as `text`,
stays plain, and `Highlight="false"` turns it off. The code must be plain text or a string expression
(`@snippet`); content containing markup is shown as written. The colors are `--ib-code-*` tokens, each
at least 4.5:1 on the block in light and dark.

The built-in highlighter is a set of small lexers, not full parsers: it colors keywords, strings,
comments, numbers, types, calls, tags and attributes, and rare constructs may stay plain. To use another
highlighter (for more languages), implement `ICodeHighlighter` and register it as a service;
`Code` uses it in place of the built-in one.

## Icons

`<Icon Name="Lucide.House" />` renders inline SVG on the server. There's no icon font, it works on
static pages, and it follows the text color (`currentColor`) and size (`em`).

- All of Lucide (1,890 icons) is bundled as one typed class with IntelliSense: `Lucide`, named as
  on https://lucide.dev/icons, where you can browse them (or on the dev host's Icon page).
  IntelliSense lists each icon's search terms. Every icon is its own property, so nothing is created
  for icons a page doesn't use, and a trimmed WebAssembly app keeps only the icons it references.
- Icons are decorative (`aria-hidden`) unless you give them a `Label`.
- `Size` is relative to the text (1em by default), `Color` overrides the text color, `StrokeWidth`
  thins or thickens a stroked icon, and `Spin` rotates it. Inside a control (a button, field, tab,
  pagination or nav item), an icon follows the control's size instead: 16, 20 or 24px for small,
  medium and large. A `Size` still wins. IzzyBlazor draws Lucide with a 1.5-unit stroke (Lucide's own
  default is 2), which gives 1, 1.25 and 1.5px lines at those sizes: crisp on a standard-density
  screen and light enough for regular text. Set `--ib-icon-stroke` on any element to change it for
  the icons inside.
- IzzyBlazor's own components use semantic roles (`<Icon Role="IconRole.Close" />`) resolved through
  `IconSet.Current` (Lucide for now), so another icon set can be swapped in later without touching
  components.
- `Brands` is a short list of the logos sites link to most, by their usual names: `GitHub`, `GitLab`,
  `YouTube`, `X`, `Bluesky`, `Mastodon`, `Discord`, `Reddit`, `Instagram`, `Facebook`, `NuGet`, `Npm`
  and `DotNet`. They're outlines drawn like the rest of the set: Tabler's, since Lucide no longer
  includes brands, with NuGet and .NET drawn for IzzyBlazor to match. The logos are their owners'
  trademarks; follow each brand's guidelines.

**Your own icon.** An icon is an `IconData`: a view box, SVG drawing elements, and whether it's
drawn with fill or stroke. For a logo `Brands` doesn't have, copy its path from https://simpleicons.org
(solid) or your designer's SVG into a static property and use it like the rest:

```csharp
public static class MyIcons
{
    public static IconData Acme => new("0 0 24 24", "<path d=\"M12 2 2 22h20z\"/>", IconStyle.Fill);
}
```

`<Icon Name="MyIcons.Acme" Label="Acme" />`. Only SVG drawing elements are allowed in the body
(paths, shapes, groups), so an icon can't carry script.

## Buttons

`Href` makes a button a link: without it you get a native `button`, with it an `a`.

- `Button` is a native `button`, or with `Href` an `a`, for navigation. A link's `External` opens a
  new tab, with an icon unless `HideExternalIcon` or an `EndIcon` is set; screen readers always hear
  `NewTabLabel`. A link can't be `Submit` or `Reset` or take `Name`, `Value`, `Form` or
  `FormAction` (it throws). A disabled or loading link has no `href`.
- `IconButton` shows only an icon. Its required `Label` becomes the accessible name and tooltip.
  With `Href` it's a link.

They share:

- `Accent` (`Primary` by default, `Neutral`, `Danger`) and `Variant` (`Solid` by default, `Soft`,
  `Outline`, `Ghost`, `Link`); `Accent.Of("#0f766e")` or `Accent.Token("success")` for a custom color,
- `Size` (`ControlSize`, shared with form fields),
- `StartIcon`/`EndIcon`, `Loading` (with `LoadingLabel`), `Disabled` and `FullWidth`.

A `Button` or `IconButton` does nothing by itself unless it's `Submit` (submits its form) or
`Reset` (resets it): `<Button Submit>Save</Button>`. On static pages a submit button posts like
plain HTML, including `Name`/`Value` to tell several submit buttons apart, and `Form` and
`FormAction` work as the native attributes do. `OnClick` runs C# on interactive pages.

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

Fields (`TextField`, `TextArea`, `NumberField`, `Slider`, `ColorPicker`, `Select`, `Combobox`,
`Checkbox`, `Switch`, `RadioGroup`, `SegmentedControl`, `CheckboxGroup`, `MultiSelect`):

- take their label, required marker, input type and length limits from the model's DataAnnotations,
- wire up `aria-describedby` and `aria-invalid`,
- show their own errors.

A field without `[Display(Name)]` gets its label from the property name split into words
(`ZipCode` becomes "Zip code"), but the validator's messages use the name as written ("The ZipCode
field is required."). Give a property `[Display(Name = "Zip code")]` when its messages should read
like its label.

An attribute's own `ErrorMessage` is used before posting too: with
`[Required(ErrorMessage = "Enter your email")]`, the browser's check says "Enter your email", not
its built-in "Please fill out this field", so the message matches the one the validator shows
after posting. This covers `[Required]`, `[StringLength]`, `[MinLength]`, `[MaxLength]`, `[Range]`,
`[EmailAddress]` and `[Url]`; attributes with the default message keep the browser's text, and
so does every field with JavaScript off. To word one form differently, set the message on the
field: `data-ib-required="…"` (also `data-ib-length`, `data-ib-range`, `data-ib-type`).

Every field also takes `Hint` (help text under it, linked for screen readers), `Required`,
`Disabled`, `ReadOnly` and `Size`. `Required` on the field marks it and asks the browser to check
it, but the server's validator only reads `[Required]` on the model, so put rules that must hold
there. `HideLabel` keeps a field's label for screen readers but doesn't show it, as in a one-field
newsletter form or a toolbar. (Checkboxes and switches show their label beside the box.)

Fields and `FileUpload` also take the item props (`<TextField Grow />`, `Width`, margins,
`AlignSelf`), `HiddenFrom`, `VisibleFrom`, `Class` and `Style`, all on the wrapper around the label,
control and hint: the element a `Row` or `Stack` lays out.

On the `Form`, `Layout="FormLayout.Horizontal"` puts labels in a column to the left of the fields
from 640px up, and `Gap` spaces the fields (5 by default). `NativeValidation` (on by default) lets the
browser check required fields and lengths before posting, `Enhance` (on by default) uses Blazor's
enhanced form handling, and `Validator` replaces the DataAnnotations validator.

**Read-only, disabled and marks.** `ReadOnly` on the `Form` makes every field read-only, as on a
page that shows a record before editing it: the values show and post, but don't change. Read-only
fields can still be focused, and screen readers say they're read-only (checkboxes, radios and
sliders only with JavaScript; see below). HTML has no read-only
select, checkbox or radio, so:

- `Select`, `Combobox` and `MultiSelect` show their value in a read-only text field ("Pro plan",
  or "Design, Writing" for several).
- `Checkbox`, `Switch`, `RadioGroup`, `SegmentedControl`, `CheckboxGroup` and `Slider` stay as they are, with
  `aria-readonly`, and neither clicks nor keys change them. Without JavaScript they're disabled
  instead, so they can't change but can't be focused either.
- Their values post from hidden inputs. A `FileUpload` is disabled, since a file can't be read-only.
  A read-only `ColorPicker` can be focused through its hex box, which needs JavaScript; without it
  only the color swatch shows, disabled.

`Disabled` on the `Form` disables every field, and disabled fields post nothing. Neither touches
buttons, so show the ones that fit. A field's own `ReadOnly` and `Disabled` work the same way. `RequiredMark` chooses the
marks beside labels: `Required` (an asterisk on required fields, the default), `Optional`
("(optional)" on the others, from `IzzyText.Optional`; clearer when most fields are required) or
`None`. A checkbox or switch is never marked "(optional)": unticked is already an answer.

**Columns.** `Columns="2"` (up to 4) lays the fields out in columns, sized by the form's own width
rather than the screen's, so the same form fits a page, a dialog and a drawer: two columns from
32rem, three from 48rem, four from 64rem, and one below that. It defaults to 1, which is the plain
stacked form. Each field takes one column, and anything else (text, `FormErrors`, a row of buttons, a
`Fieldset`) takes the whole row. `ColSpan` changes either: `ColSpan="2"` widens a field, and
`ColSpan="GridSpan.Full"` gives it the row. A span never goes past the columns shown, so a form that
narrows to one column needs no breakpoints. `NewRow` starts a field on a new row, leaving the rest of
the row before it empty. A `Fieldset` takes `Columns` too, for a group laid out
on its own (an address in three columns inside a two-column form). A form with columns fills its
parent's width (in a `Row`, the space left beside the rest), since its columns follow that width;
`Width` or `Basis` sizes it. With `Layout="FormLayout.Horizontal"`, fields in columns keep their
labels on top, as there's no room beside them.

```razor
<Form Model="Model" FormName="contact" Columns="2">
    <TextField @bind-Value="Model.FirstName" />
    <TextField @bind-Value="Model.LastName" />
    <TextField @bind-Value="Model.Street" ColSpan="GridSpan.Full" />
    <TextField @bind-Value="Model.City" />
    <Select @bind-Value="Model.Region" />
    <Row><Button Submit>Save</Button></Row>
</Form>
```

- `Fieldset` groups related fields under a `Label` (its `legend`), with an optional `Description`.
  It takes the style props, so `Border="1" Padding="4" Radius="Radius.Md"` draws a boxed group.
  `Dividers` draws a line between its rows, with the `Gap` above and below it, as in a list of
  settings. Each direct child is a row, so don't put `Divider`s between them, and a row's own
  `Padding` replaces the space above its line. A first row hidden with `HiddenFrom` or `VisibleFrom`
  still counts as first, so the next row keeps its line. It's ignored when `Columns` is more than 1.
- `FormActions` is the row of buttons at the end: across the whole row, buttons at the end
  (`Justify` moves them). Put them in reading order, Cancel before the main action. Last in a form
  that fills a `Dialog` or `Drawer`, it becomes the dialog's footer, like `DialogFooter`: it
  reaches the edges and stays in view while the body scrolls, and its buttons still submit the form.
- `FormErrors` lists every validation message in the form, under a `Title` ("Please fix the
  following"). After a failed submit it takes focus, with JavaScript off too, so keyboard and screen
  reader users start from the list of problems.
- `FieldError For="() => Model.Email"` shows one property's messages, for your own inputs.
  IzzyBlazor fields show their own.
- `FieldFrame` is the frame every field uses: `Label`, `Hint`, `Errors` and the required marker
  around your own control (`Id` links the label to it; `Group` renders a `fieldset`). Wrap a custom
  field in it to match the built-in ones.

**TextField and TextArea.** `TextField`'s `Type` (`Text`, `Email`, `Password`, `Search`, `Tel`, `Url`)
comes from `[EmailAddress]`, `[Phone]`, `[Url]` or `[DataType(DataType.Password)]` when not set. It
takes `StartIcon`, `EndIcon` and `AutoComplete`; a password field gets a show/hide button once
scripts run (`Reveal`, on by default). `TextArea` shows 4 `Rows` by default; `AutoGrow` grows it
with the text where the browser supports `field-sizing`, and `ShowCount` shows "n / max" under it.

`Select` and `RadioGroup` list enum values automatically, using `[Display(Name)]`.

**NumberField.** A native number input bound to any number type (`int`, `decimal`, `double`, and
their nullable forms), so it posts with the form, the browser checks `Min`, `Max` and `Step`, and phones
show a number keyboard. `Min` and `Max` default to the model's `[Range]`; `Step` defaults to 1 for
whole numbers and to any value for decimal types (set `Step="0.01"` for cents). Values always use
invariant formatting (`1.5`), as number inputs require, whatever the page's culture. With JavaScript,
stepper buttons appear: hold one to repeat, and each end disables at `Min` or `Max`.
`StepButtons` places them: `StepButtons.Stacked` at the end (the default), `Split` (a full-height
button at each end, easier to tap) or `None`. With JavaScript off, a field with steppers shows the
browser's own spin buttons in their place (`None` shows neither), and arrow keys step either way. It takes the same
`StartIcon` and add-ons as `TextField`.

**Slider.** A native range input bound to a number type, so it posts with the form and takes arrow
keys, Page Up/Down and Home/End with no script. `Min` and `Max` default to `[Range]`, or 0 to 100;
`Step` to 1. With JavaScript the track fills up to the thumb and the value shown by `ShowValue`
follows as you drag. Without it neither could follow, so the track is plain (Firefox fills it
itself) and the value isn't shown. `ValueFormat` sets how the value reads, both shown and announced to screen
readers: `"{0}%"`, `"${0}"` (without JavaScript, screen readers hear the plain number, which
unlike formatted text stays current). On interactive pages the bound value updates on release; `Live` updates
it while dragging, at one round trip per move. A read-only slider stays focusable with
`aria-readonly` and posts its value through a hidden input.

**ColorPicker.** A color field bound to a hex string, which it keeps as `#rrggbb` in lowercase
(`#ABC` and `abc` are read as `#aabbcc`). Underneath is a native color input, so it posts with the form
and works with no script: the swatch fills the field and opens the browser's picker. With JavaScript
the swatch moves to the start and a hex box appears beside it for typing or pasting (`ShowHex`, on by
default). `Presets` lists colors offered as swatches under the field, and clicking the swatch in the field
opens the picker directly; without script, Chrome and Edge offer the presets in a menu before their
picker instead. A color input always has a value, so an empty field shows and posts
black (`#000000`). Outside a `Form`, give it a `name` to post with a plain form. A read-only picker is
disabled and posts its value through a hidden input.

```razor
<ColorPicker @bind-Value="Model.Brand" Presets="@(["#2563eb", "#0f766e", "#be123c"])" />
```

**FileUpload.** A drop zone around a real file input, named by `Name`: "Choose a file or drag it
here", with a hint under it.

- **No JavaScript:** "Choose a file" is a `<label>` for the input, so it opens the picker; the
  browser names the chosen file under it, and the file posts with the form. Set `Multipart` on the
  `Form` (it sets `enctype="multipart/form-data"`; without it only file names post), and bind the
  files on the page with `[SupplyParameterFromForm(FormName = "...")]`: `IFormFile` for one,
  `IFormFileCollection` for `Multiple`, named like the field.
- **With JavaScript:** files can be dropped anywhere on the zone, and the chosen file's name and size
  (or "3 files") replace the hint. Dropped files that don't match `Accept` are refused, and a picked
  file of the wrong type (the picker's "All files" lets one through) or over `MaxFileSize` is flagged
  before the form posts. A form that isn't multipart logs a console warning.
- **Interactive pages:** `OnChange` receives the chosen files as `IBrowserFile`, up to `MaxFiles`
  (10 by default) and leaving out any over `MaxFileSize`. Read one with
  `OpenReadStream(maxAllowedSize)`: its default limit is 500 KB. Nothing posts.

The hint comes from `Accept` and `MaxFileSize`: `Accept=".png,.jpg" MaxFileSize="2_000_000"` reads
"PNG or JPG · up to 2 MB". Types are listed only when each has a plain name, so `"image/*"` shows just
the size. `AcceptText` replaces the hint (`""` hides it); `ChooseText` and `DragText` replace the
prompt's two parts. On touch screens the zone shows only the link. `Hint` is help text under the
zone, as on other fields.

`Accept` filters the picker (`"image/*"`, `".pdf"`), but always check types and sizes on the server,
and raise the server's request size limit to match `MaxFileSize`. `Errors` shows server-side
messages under the zone. The zone's look is set with `--ib-upload-bg`, `--ib-upload-border-color`
and `--ib-upload-border-style` (see [Customizing](styling.md#customizing)).

```razor
<Form Model="Model" FormName="profile" Multipart OnValidSubmit="SaveAsync">
    <FileUpload Name="Avatar" Label="Profile photo" Accept=".png,.jpg" MaxFileSize="2_000_000" />
</Form>

@code {
    [SupplyParameterFromForm(FormName = "profile")] public IFormFile? Avatar { get; set; }
}
```

**Add-ons.** `TextField` and `NumberField` take `StartAddon` and `EndAddon`: content attached to the control, outside its
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
The closed field keeps a long value on one line: put the text in its own element
(`<span>@plan.Name</span>`) and it ends in "…"; otherwise it's cut off at the field's edge.
Either way, hovering the field shows the chosen text as a tooltip (a `title` on the field's wrapper,
so screen readers don't hear it twice).

**Combobox.** A select you can type in.

- **No JavaScript:** it's a native select, so the form still posts.
- **With JavaScript:** typing filters the list, ignoring case and accents, and arrow keys and Enter
  choose. The hidden select keeps posting the value, so `@bind`, required validation and labels work
  unchanged.
- **Options:** give it `Items` (or an enum), or `Search` for long lists. `Search` is called with `""`
  for the first list, and as the user types on interactive pages (debounced, and canceled when
  superseded).

**Checkbox, RadioGroup and Switch.** Checkboxes and radios are drawn with the field colors, so
they match fields and switches in every browser and in dark mode, and they follow `Size`. In
Windows high-contrast mode they fall back to the browser's own boxes. `Checkbox Indeterminate`
shows a dash, for a "select all" box when only some items are selected. It's display only: the box
posts as unchecked and choosing it clears the dash. CSS shows it with no script; with script,
screen readers announce it as "mixed". Work it out from the other values, so it's right after a
post too.

On `Checkbox` and `Switch` (not the options of `RadioGroup` or `CheckboxGroup`),
`ControlPosition="ControlPosition.End"` puts the box or switch at the end of the row, centered on
the label and hint, with the hint under the label. The label names it, and errors show under the
hint without moving it. In either position, a click anywhere on the row (the icon, label, hint or
the gap beside the control) toggles it; a link in the hint stays a link. Together with `Fieldset Dividers` it makes a
settings list:

```razor
<Fieldset Label="Email notifications" Dividers Gap="3">
    <Switch @bind-Value="Model.NewDevice" ControlPosition="ControlPosition.End"
            Hint="An email each time, with a link to sign that device out." />
    <Switch @bind-Value="Model.Mentions" ControlPosition="ControlPosition.End"
            Hint="When someone mentions you or replies to you." />
</Fieldset>
```

`Icon` on a `Checkbox` or `Switch` puts an icon before the label; at the end position it's at the
start of the row, centered on the label and hint. `Icon` on a `Fieldset` puts one at the start of
its heading, centered on the label and description, to tell sections apart. Both are decorative
(screen readers read only the text). Use icons on the rows or on the sections, not both, and on
every row of a group or none.

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
  `SelectAll` adds a box above the options that checks or clears them all at once and shows a dash
  when only some are checked (`SelectAllText` for its text, "Select all" by default). It doesn't
  post; the options do. The script runs it, on static pages too, so with JavaScript off it isn't
  shown. On an interactive page `ValueChanged` runs once for each option it flips.
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

- `AppHeader` (`Position`: sticky by default; `ContentWidth`)
- `AppBrand`, the name in the header (`Label`, `Href`, `Icon` or `Logo`, `HideLabel` for a logo that
  spells the name). It stays on one line: on a narrow screen the name ends in an ellipsis instead of
  wrapping.
- `AppSidebar` (`Position` start or end, `DrawerPosition`, `SmallScreen`, `FullHeight`, `Width`, `Breakpoint`)
- `AppMain` (`ContentWidth`)
- `AppAside` (`Width`, `Breakpoint`), for `OnThisPage` or other side content
- `AppFooter` (`Position`, `ContentWidth`)

Every part takes `Bg` and `ColorScheme`, and the sidebar and aside colors run the full height. `AppShell`
itself sets `ScrollMode` (`Page`, or `Main` for a window-filling frame where only the content
scrolls), `Dividers`, and `Variant`, how the parts are layered. Content is the brightest layer and
the frame recedes into the page tint: `Tinted` (the default) tints the sidebar, `Inset` tints the
whole frame and raises the main area as a rounded panel, `Flat` is all surface with lines, and
`Canvas` tints the main area for dashboards whose content is all cards. A part's `Bg` still wins. The shell arranges itself from the parts present with CSS alone (`:has()`),
so it renders in one pass on static pages.

**Content width.** A reading site keeps its header, text and footer on one centered column.
`ContentWidth` on `AppShell` (such as `Length.Rem(42)`) centers the content of the header, main area
and footer at that width by widening their side padding, never below the page gutter, so the edges
line up while the bars, backgrounds and lines stay full width. `AppHeader`, `AppMain` and `AppFooter`
each take their own `ContentWidth` to override it, for a wider header over a narrow article:

```razor
<AppShell ContentWidth="Length.Prose">
    <AppHeader ContentWidth="Length.Rem(64)">...</AppHeader>
    <AppMain>@Body</AppMain>
</AppShell>
```

With a sidebar, the main area centers in its own column but the header spans the window, so set
`ContentWidth` on `AppMain` alone. The main area puts its content in a centered column, and a
`Section` inside it spans the whole main area (see [Layout](#layout)).

**The drawer.** Below its breakpoint the sidebar becomes a drawer opened by `SidebarToggle`. It works
with JavaScript off (a link to the sidebar and CSS `:target`); with JavaScript it also closes on
Escape, backdrop click and navigation. On phones (below 480px) it takes the full width, with its close
button where the menu button was; on wider screens it is the sidebar's width or 360px, whichever is
wider. `DrawerPosition` sets the drawer's side on its own: it follows `Position` unless set, so
`DrawerPosition="SidebarPosition.End"` keeps a sidebar on the left on wide screens but opens it from
the right on small ones, and moves `SidebarToggle` to the end of the header there, where content
sites put the menu button.

The drawer's top row is its `SidebarHeader`, with the close button in it. A sidebar with no
`SidebarHeader` shows the shell's `AppBrand` there instead (the first one in the shell), as the
header does, so the drawer never opens on an empty bar. To show something else there, give the
sidebar a `SidebarHeader`. `SidebarToggle`'s `CloseLabel` names the close button ("Close menu").

**A panel instead of a drawer.** `SmallScreen="SidebarSmallScreen.Panel"` opens the sidebar under
the header below its breakpoint, as `SiteNav`'s menu does: full width and as tall as its content (up
to the bottom of the window, then it scrolls), with the header still in view and `SidebarToggle`
turned into its close button. The rest of the page is dimmed, and a tap there, Escape or following a
link closes it; it works with JavaScript off too. `SidebarHeader` and `SidebarFooter` sit at the
panel's top and bottom. Use it for a site whose sidebar is a short list of sections; keep the drawer
(the default) for long navigation such as docs or an app. `DrawerPosition` still moves the menu
button to the end of the header. The panel is placed under the header with CSS anchor positioning;
where that isn't supported, under the sticky banner and header heights.

**Header links on phones.** A site with no sidebar puts its links in `SiteNav`. From its
`Breakpoint` up (`Md` by default) they are a row in the header; below it they fold into a menu
button that opens the same links in a panel just under the header, with 44px rows. The header stays
in view, with the site's name, and the same button closes the panel (its icon turns into a close
icon). `SiteNavFooter`, written among the links, adds content under them in the panel (social
links, a call to action). The menu button moves to the end of the header on phones
(`MenuButtonAtEnd="false"` keeps it in place).

```razor
<AppHeader>
    <AppBrand Href="/" Label="Acme" />
    <Box Grow />
    <SiteNav Label="Site">
        <NavItem Href="/projects">Projects</NavItem>
        <NavItem Href="/articles">Articles</NavItem>
        <SiteNavFooter><Button Href="/signup" FullWidth>Sign up</Button></SiteNavFooter>
    </SiteNav>
    <IconButton Icon="Brands.GitHub" Label="GitHub" Href="https://github.com/acme" Variant="ButtonVariant.Ghost" />
</AppHeader>
```

The panel is a native popover, so it opens, closes on Escape and closes on a click outside with
JavaScript off. It lines up under the header with CSS anchor positioning; where that isn't
supported, it sits under the shell's header height. With JavaScript it also moves focus to the first
link, and closes after a link is followed (enhanced navigation keeps the page) or when the screen
grows past the breakpoint. The items render twice (row and panel), so keep them to links. With
JavaScript off, a link to a section of the same page (`#features`) leaves the panel open. With
`MenuButtonAtEnd`, the button is drawn at the header's end but keeps its place in the Tab order, so
for keyboard users put the SiteNav last in the header, or set `MenuButtonAtEnd="false"`.

With an `AppSidebar` in the shell there is one menu, not two: SiteNav's button and panel stay
hidden, and below its breakpoint its links show at the top of the sidebar, above a line (in the
drawer on phones, or in the sidebar column if SiteNav's breakpoint is the wider one). A
`SiteNavFooter` doesn't move with them: put that content in the `SidebarFooter`.

**Sidebar header and footer.** `SidebarHeader` and `SidebarFooter`, written directly in the
`AppSidebar` among its content, are pinned to its top and bottom (a workspace switcher, an account
menu), with the content scrolling between them. They have no lines at rest; a line shows next to one
only while content is scrolled out of view there (CSS only). They're drawn at the sidebar's level, so
a form or cascading value they're written inside doesn't reach them. `SidebarBody` (optional) is the
scrolling content with `Stack`'s layout props, such as a `Gap` between several `NavMenu`s.

```razor
<AppSidebar Collapsible>
    <SidebarHeader><AppBrand Href="/" Icon="Lucide.Zap" Label="Acme" /></SidebarHeader>
    <NavMenu>...</NavMenu>
    <SidebarFooter><NavMenu aria-label="Account">...</NavMenu></SidebarFooter>
</AppSidebar>
```

Under a full-width `AppHeader`, the brand belongs in the header and the `SidebarHeader` suits a
workspace switcher. With `FullHeight`, the `SidebarHeader` is the top-left corner, so put the brand
there. Its row then matches `AppHeader`'s height, with a line that continues the header's. In the
drawer, the `SidebarHeader` row is the top bar, with the close button in it.

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
  sidebar, and `SidebarHeader`/`SidebarFooter` content stacks and centers. An `AppBrand`'s name
  hides there by itself (it shows in the drawer and the full sidebar), so a brand needs no
  `HideLabel`. Mark other text with the `ib-collapsed-hide` class (or `ib-collapsed-show` for
  rail-only content).

**Navigation.** `NavMenu` is a list of links, vertical by default (`Orientation.Horizontal` for a
header). `NavItem`s work out their active state on the server: `Match` is `Prefix` by default, so
pages below the link count too (`NavLinkMatch.All` for an exact match; a root link always needs
one). A link with a query string is current only with the same query, and of links to one page
told apart by the query (`?view=starred`), one is current: the link without a query gives way when
one with a query matches, in a `NavMenu` or a `SiteNav`. An item takes an `Icon` and a `Badge` (`BadgeStatus`, `BadgeAccent`, `BadgeVariant`;
`BadgeVariant.Plain` is a quiet count with no fill, as docs and mail sidebars show totals). Long
titles wrap in a vertical menu, keeping the space a one-line item has above and below; a horizontal
menu keeps them on one line. `NavLabel` is a small
heading and `NavDivider` a line. `NavGroup` is a collapsible group with a `Label` and `Icon`, built
on `details`: it starts open with `Open` or when the current page is under its `ActivePrefix`, and
with JavaScript any group holding the current page opens itself. With JavaScript, a sidebar whose
current link is out of view when a page opens (a long menu, a shared link) scrolls its list to show
it; the page itself doesn't move.

```razor
<NavMenu>
    <NavLabel>Workspace</NavLabel>
    <NavItem Href="/" Icon="Lucide.House" Match="NavLinkMatch.All">Home</NavItem>
    <NavGroup Label="Projects" Icon="Lucide.Folder" ActivePrefix="/projects">
        <NavItem Href="/projects/active">Active</NavItem>
    </NavGroup>
</NavMenu>
```

**On this page.** `OnThisPage`, in an `AppAside`, lists the page's sections. Each `Heading` in
`AppMain` adds itself while the page renders, so the list is in the first response and works with
JavaScript off; once an `OnThisPage` is on the page, headings get an `id` slugged from their text
("Rows & columns" becomes `rows-columns`, a repeat `-2`) unless they set one. `MinLevel` and
`MaxLevel` (2 and 3 by default) are the heading levels listed; lower levels are indented. Headings it
lists also link to themselves: the heading's text becomes a link to its section, with a "#" after it
on hover or focus (`Anchor="true"` or `"false"` on `Heading` turns that on or off). A heading that holds
a link or button of its own (an `a`, an `Anchor` or a button) never gets one, since links can't
nest; one that holds another component (other than `Icon`, `Badge`, `Kbd` or `TextBlock`) gets one
only with `Anchor="true"`, since it might render a link. Headings in a `CodeExample`'s preview, a
dialog or a popover aren't sections and aren't listed. `Outline="false"` leaves a heading out. Linked sections stop below a sticky header.
The list has its own rail, so the aside drops its divider while it shows one.

```razor
<AppMain>@Body</AppMain>
<AppAside>
    <OnThisPage />
</AppAside>
```

With JavaScript, `OnThisPage` marks the section in view (`aria-current="location"`) as the page
scrolls, and adds headings with ids that aren't `Heading` components, such as those in `Prose` or
rendered Markdown. Two limits of the server's list, which the script fixes: a heading inside another
component (a `Card`, a `Section`) is listed after the headings around it, since components render
their content after their parent's; and with no `Heading` components the list is hidden until the
script fills it in. Headings in an interactive page under a static layout aren't seen by the shell,
so only those with their own `id` are listed (by the script).

`SidebarToggle` opens the sidebar whose id is its `For` (`ib-sidebar` by default, the sidebar's
default `Id`), so a page with two sidebars gives each its own `Id` and toggle.

## Disclosure

`Accordion` with `AccordionItem`s, and `Collapsible` for a single "show more". Both are native
`details`, so they open and close with JavaScript off. `Exclusive` uses the shared `name` attribute to
keep one item open, and heights animate with no script where the browser supports it.

- `Accordion`: `Variant` (`Separated` cards by default, `Bordered` one box, `Flush` lines only),
  `Size` (`Lg` for FAQ pages), and `ContentPadding`/`ContentGap` for every item's content (`0`
  padding lets a table run edge to edge). `Name` groups several accordions as one exclusive set.
- `AccordionItem`: `Title` (or an `AccordionHeader` part for inline markup), `Icon`, `Disabled`, `Open`, and its own
  `ContentPadding`/`ContentGap`. Give every item one or the other: the heading is the item's only
  name for screen readers.
- `Collapsible`: a `Label` after a chevron, or your own `Trigger` content.

## Overlays

`Menu`, `Popover` and `Tooltip` use the Popover API and CSS anchor positioning. They open, close on
Escape and click-outside, and sit next to their trigger with no script. `Placement` puts them
below, above or beside the trigger (`BottomStart` by default, `Top` for tooltips).

- `Menu` and `Popover` render a button from `Label`, `StartIcon`, `Accent` (`Neutral`), `Variant`
  (`Outline`) and `Size`, or take your own `Trigger`.
- `Popover` is non-modal content with `role="dialog"`, such as filters or a small form. `Title` adds
  a heading; `Mode="PopoverMode.Manual"` keeps it open until its trigger or close button closes it
  (`ShowCloseButton`).
- `Tooltip` wraps its trigger and shows `Text` (or `Content`) on hover and keyboard focus, after
  `Delay` (300ms). With JavaScript off it's pure CSS. Keep essential content out of tooltips: touch
  screens can't hover.
- `PanelWidth` sets a `Menu` or `Popover` panel's width, such as `Length.Rem(32)` for a filter form.
  Without it a panel fits its content, up to 24rem. It never gets wider than the screen.

The layout props (`Grow`, margins, `AlignSelf` and the rest) go on the element that wraps the trigger
and panel, so a menu can grow or take a margin in a toolbar. `Class` and `Style` go on the panel.

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

A `Dialog` has a `Title`, content and a `DialogFooter`; `DialogClose` is a button that closes it.
`Size` is `Sm` (24rem), `Md` (32rem, the default), `Lg`, `Xl` or `Full`. `CloseOnBackdrop` and
`ShowCloseButton` are on by default. `QueryKey` changes the `?dialog=` key.

```razor
<DialogTrigger For="confirm-delete" Accent="Accent.Danger">Delete</DialogTrigger>
<Dialog Id="confirm-delete" Title="Delete this project?" Size="DialogSize.Sm">
    This can't be undone.
    <DialogFooter>
        <DialogClose>Cancel</DialogClose>
        <Button Submit Form="delete-form" Accent="Accent.Danger">Delete</Button>
    </DialogFooter>
</Dialog>
```

Parts are written among the content:

- `DialogFooter` is the row of buttons at the bottom, with `Row`'s props (`Justify` moves the
  buttons; they're at the end by default). It's drawn where it's written, so write it last: last in
  the dialog, or last in a form that's last in the dialog. In a form, its submit button submits the
  form with no `Form="id"`, with JavaScript off too. It reaches the dialog's edges and stays in view
  while the body scrolls. On its own it follows the content; after a `DialogBody` it sits at the
  bottom of a side drawer however short the content is.
- `DialogBody` (optional) is the body with `Stack`'s layout props. It fills the dialog's height, so
  the `DialogFooter` after it sits at the bottom of a drawer; in a form, put both in the form.
  `Flush` runs it to the dialog's sides, for a table or a list. With a `DialogBody`, put all the
  body's content in it: text written beside it is laid out as separate rows.
- `DialogHeader` is heading content in place of `Title` (a title with an icon or badge). It's drawn
  in the header row beside the close button and names the dialog for screen readers. Like
  `CardHeader`, a form or cascading value it's written inside doesn't reach it.

```razor
<Dialog Id="rename" Title="Rename project">
    <Form Model="model" OnValidSubmit="Save">
        <TextField @bind-Value="model.Name" />
        <DialogFooter>
            <DialogClose>Cancel</DialogClose>
            <Button Submit>Rename</Button>
        </DialogFooter>
    </Form>
</Dialog>
```

`Drawer` is a `Dialog` that slides in from an edge: the end side by default, or `Placement` `Start`,
`Top` or `Bottom` (a sheet along the bottom suits phones). It opens and closes the same ways and
traps focus the same way. `Size` sets a side drawer's width (`Sm` 20rem, `Md` 24rem, `Lg` 32rem,
`Xl` 40rem, `Full`); top and bottom sheets are as tall as their content, up to 85% of the screen.
Sides flip in right-to-left languages.

For a width the sizes don't cover, set `Width` on a `Dialog` or side `Drawer`, such as
`Width="Length.Rem(56)"` for a dialog holding a table; it can change per breakpoint. It replaces the
`Size`'s width and keeps its margin on small screens (1rem each side of a dialog, 3rem beside a
drawer). Top and bottom sheets span the screen and `Full` fills it, so they ignore it. A dialog isn't
laid out in the page, so it takes no other layout props: margins, `MinWidth`, `MaxWidth` and the flex
props throw. `ContentPadding` replaces the body's padding (`0` runs a table or image edge to edge).

**Dialogs from code (interactive pages).** `DialogService` opens a component in a dialog and awaits
how it ends, for flows like "edit this, then refresh":

```razor
@inject DialogService Dialogs

var result = await Dialogs.OpenAsync<EditSite>("Edit site", new() { ["Site"] = site });
if (!result.Canceled) await ReloadAsync();

if (await Dialogs.ConfirmAsync("Ada loses access.", "Remove member?",
        new() { ConfirmText = "Remove", Accent = Accent.Danger })) { ... }
```

The component gets a cascading `DialogInstance` to close with data (`Dialog.Close(site)`) or cancel;
Escape, the backdrop and the close button cancel too (`result.Canceled`). Put its buttons in
`DialogFooter`, last, for a footer row. `DialogOptions` set the size, placement (a drawer or sheet)
and closing behavior, and the `ColorScheme`, `Brand` and `Density`: the host shows the dialog, not the part of
the page that opened it, so a dark or compact part passes its own. It needs a `<DialogHost />` on the
interactive page or in an interactive layout (`@rendermode` on the host where the layout is static);
with more than one, the newest shows the dialogs. Static pages can't open anything from
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

Menu items are links (`Href`), form posts (`Submit`, with `FormAction`, `Name`
and `Value`) or C# actions (`OnClick`). An item takes an `Icon`, a `Shortcut` shown at the end in a key cap (display only),
`Accent.Danger` for a destructive action, and `Disabled`. `MenuGroup` gives a few items a heading
and `MenuDivider` draws a line. Menu roles (`menu`, `menuitem`) promise arrow keys,
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

- `QueryKey="tab"` keeps the tab in the URL (`?tab=billing` opens Billing). A tab's `Value` defaults
  to its label in lower case with dashes (`billing-history`).
- `@bind-Selected` follows it on interactive pages.
- `Variant` (`Line`, `Soft`, `Segmented`), `Size` and `FullWidth`; each `Tab` takes a `Label`, `Icon`, `Badge` and `Disabled`.
- `ContentPadding` pads every side of each panel, in place of the default space above and below (`0`
  lets a table run edge to edge); a `Tab` can set its own.
- `Orientation="Orientation.Vertical"` puts the tabs in a column beside the panels from 640px up,
  and Up and Down arrows move between them. On phones they stay a scrolling row above the panels.

**Tabs in a form.** Every panel is part of the form, so every field posts, whichever tab is open.
After a submit that fails, the first tab with errors opens, unless the open one has some, and each
tab shows how many errors its fields have, which screen readers hear with its name ("Address, 2
errors"; `IzzyText.OneError` and `ErrorCount`). Only a submit switches tabs, never an error that
appears while someone types. On a static page the server doesn't know which tab was open when the
form posted, so it counts the first tab (or the `QueryKey` one) as open.

The browser checks fields before posting and points at the first invalid one, even in a closed
tab: with JavaScript, that tab opens. Without it, tabs in a form show every panel in order, each
under its tab's label, since the browser can't point at a field in a hidden panel. Errors found by
the server after posting open their tab as above. Fields count toward their
tab, and toward the outer tab when tabs are nested. A `FileUpload`, Blazor's own `Input*`
components and fields of your own don't count toward a tab, since they aren't IzzyBlazor fields;
their errors still show beside them.

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

Plain links, so neither needs script. `Breadcrumb` holds `BreadcrumbItem`s; the last one, with no
`Href`, is the current page. `Separator` is `Chevron` (the default, mirrored in right-to-left
languages) or `Slash`.

`Pagination` takes `PageCount`, or `TotalItems` with `PageSize` (20 by default). It reads and writes
`?page=` (`QueryKey`; page 1 leaves it out, and other parameters are kept), so the page reads it with
`[SupplyParameterFromQuery]` and paging works with no script. `Siblings` and `Boundaries` (1 each)
set how many numbers show around the current page and at the ends. On phones it shows only the
neighbors and the ends. With `@bind-Page` on an interactive page, the pages
become buttons that update C# without touching the URL; the button that was picked keeps focus as
it becomes the current page, and previous and next stay buttons at the ends.

`PrevNext` ends an article with links to the pages around it: two cards with "Previous" and "Next"
over the page titles (`PreviousHref` and `PreviousTitle`, `NextHref` and `NextTitle`), side by side
when it's 32rem wide or more and stacked below. Leave out a side on the first or last page. The
links carry `rel="prev"` and `rel="next"`; `PreviousText`, `NextText` and `Label` (the landmark,
"More pages") change the text.

```razor
<PrevNext PreviousHref="docs/install" PreviousTitle="Install"
          NextHref="docs/theming" NextTitle="Theming" />
```

## Stepper

Where someone is in a process, such as a checkout: `Stepper Current="2"` (counting from 1) around
`Step`s with a `Label`, optional `Description` and `Icon`. Steps before `Current` show a check, and
screen readers hear `CompletedLabel` ("(completed)") after their label; the current step has
`aria-current="step"`. A `Step` with `Href` is a link, to go back to a finished step, say. It's a row
from 640px up and a column on phones (`Orientation.Vertical` for a column everywhere); `Size` scales
the markers. Nothing needs script: drive `Current` from the URL or the form post. For a step that only sometimes applies, set `Hidden` rather than wrapping it in `@if`: a step Blazor adds later would otherwise be numbered after the rest.

## Alerts and toasts

`Alert` takes a `Status` (`Info` by default), `Variant` (`Soft` by default, `Outline`, `Solid`),
`Title`, `Icon` or `HideIcon`, and `Dismissible`: a close button that works with
JavaScript off, with `@bind-Dismissed` on interactive pages. With JavaScript it is a real button,
and closing moves focus to the next control on the page. Danger and warning alerts are
`role="alert"`, announced right away; the rest are `role="status"`. The status is read before the
message (`StatusLabel`), since the icon and color carry it only visually.

Buttons or links after the message go in `AlertActions`, written last in the alert (a row with
`Row`'s props):

```razor
<Alert Status="Status.Warning" Title="Unverified email">
    Check your inbox for the link.
    <AlertActions><Button Size="ControlSize.Sm">Resend email</Button></AlertActions>
</Alert>
```

Toasts appear in a `<ToastRegion />`, placed once in the layout: `Placement` (`BottomEnd` by
default, or `TopStart`, `TopCenter`, `TopEnd`, `BottomStart`, `BottomCenter`), `Duration` (5000ms)
and `Variant` (`Outline`). They come from `ToastService` (`Toasts.Success("Saved")`, also `Info`,
`Warning`, `Error` and `Show`) in any render mode:

- **A static form post** renders them in the response.
- **A redirect** carries them to the next page in a short-lived cookie.
- **Interactive pages** add them in the browser at once.

With JavaScript, toasts close after `Duration`, pausing on hover and focus; error toasts stay until
closed. Without JavaScript, they stay until closed.

Three cases lose a toast, all rare:

- **An interactive page whose connection drops** (Blazor Server) loses toasts that hadn't been shown
  yet; the page reconnects without them.
- **A redirect after the response has started streaming** (a page with `[StreamRendering]` that
  redirects partway through) can't set the cookie that carries toasts, since the headers are already
  sent. Redirect before streaming starts, or show the message on the page.
- **`NavigateTo(..., forceLoad: true)` on an interactive page** loads a whole new page, which the
  toast shown just before it doesn't survive. A plain `NavigateTo` keeps it: when the next page's
  render redraws the region (an interactive part of a static page), the script puts its open toasts
  back.

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
    <Banner Status="Status.Warning" Dismissible RememberKey="maintenance-2026-11" Remember="DismissMemory.Persistent">
        Scheduled maintenance on Sunday from 02:00 to 04:00 UTC.
        <BannerActions><Anchor Href="/status">Details</Anchor></BannerActions>
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
  Alert. `Accent` replaces the status's color, for a brand message such as `Accent.Primary`; the status
  still sets the icon and what screen readers hear. `Icon` and `HideIcon` change the icon, `Title`
  adds a bold lead-in, and `BannerActions` holds a link or small button after the message.
- **Dismissing:** `Dismissible` adds a close button that works with JavaScript off, with
  `@bind-Dismissed` on interactive pages. With JavaScript it is a real button, and closing moves
  focus to the next control on the page.
- **Staying dismissed:** closing it writes a cookie (`ib-banner-{key}`), and the server reads it and
  leaves the banner out of later pages, static or interactive. `Remember` sets how long:
  `DismissMemory.Session` (the default) until the browser closes, `Persistent` for a year, `None`
  only on the page it was closed on. `RememberKey` names the cookie; change it to show a new message
  to everyone. Without one, the key comes from the `Title`, text and links, so a changed message
  shows again by itself, and banners with the same words and links share a dismissal. Text that
  comes from a component of your own (a localizer, say) can't be read, so such a banner is
  remembered only under a `RememberKey`. Reading the cookie needs `AddIzzyBlazor()` on the server. In WebAssembly the
  banner renders, and the script closes it as soon as it appears. With JavaScript off the close
  button works, but nothing is remembered. Keys are letters, digits, `-`, `_` and `.`.
- **Accessibility:** it's a region named "Announcement" (`Label`), so screen reader users can find
  it. It isn't announced on its own, since it's there when the page loads; set `Announce` for a
  banner you show in response to something, such as a lost connection.

## Badge

A small label for a status, category or count: `Status` (`Neutral` by default, `Info`, `Success`,
`Warning`, `Danger`) or an `Accent` in its place, `Variant` (`Soft` by default, `Solid`, `Outline`,
or `Plain`: just the text, in the color's text shade, a muted gray for `Neutral`) and `Size`
(`Sm` for counts in navigation, `Md`). `Pill` rounds the ends, for counts; `Dot` adds a colored dot
before the text, for statuses; `StartIcon` adds an icon.

```razor
<Badge Status="Status.Success">Published</Badge>
<Badge Status="Status.Danger" Variant="BadgeVariant.Solid" Pill>3</Badge>
<Badge Status="Status.Warning" Dot>Pending review</Badge>
```

## Tag

A keyword, category or applied filter: larger than a `Badge`, with the same `Status`, `Accent` and
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
  that sweeps. `Accent` and `Size` (thickness) are optional. On interactive pages, change `Value`
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
- `Size` (`Xs` to `Xl2`), `Square` for teams and companies, `Accent` for a fixed color in place of
  the name's, `Variant` `Soft` (the default: a tint with the color as text) or `Solid` (filled, text
  picked for contrast), as on Badge and Tag, `Initials` to
  override the letters, `Icon` for the no-name icon.
- `Status` adds a dot in the corner (`Success` for online, say), with `StatusLabel` ("Online")
  joined to the accessible name.

`AvatarGroup` overlaps its avatars, each ringed in the page color (set `--ib-avatar-ring` on a
tinted surface). `Max` (1 to 10) shows the first few and counts the rest in a "+N" avatar, whose
accessible name is `MoreLabel` ("{0} more"). `Size` sets every avatar's size. Put the avatars
directly inside the group, since `Max` hides the extras by counting its children.

## Card

`Title` (`TitleLevel` 3 by default) and `Description`, in `Outline` (the default), `Elevated` or
`Filled`. `Size` sets the inner spacing and title size. `Href` makes the whole card a link through
its title (one link for screen readers, so it needs a `Title`), while buttons in it stay clickable.

Content between the tags is the body. The other parts are components written directly in the
card, among its content:

- `CardMedia`: an image or video at the top, edge to edge.
- `CardHeader`: custom heading content above the `Title`, such as an avatar row.
- `CardActions`: buttons at the end of the heading row, such as a menu.
- `CardBody`: the body with `Stack`'s layout props (`Gap`, `Align`, `Justify`, `Direction`).
  Optional; it reads well when a card has several parts. `Flush` runs its content to the card's
  edges, for a table or a divided list.
- `CardFooter`: the bottom row, with `Row`'s props (`Justify` moves its buttons), drawn last. It
  spans the card's width and bottom edge, so `Bg` or `BorderTop` makes it a bar across the bottom:
  `<CardFooter Justify="Justify.End" Bg="Color.SurfaceContainerLow" BorderTop="1">`.

`CardMedia`, `CardHeader` and `CardActions` are drawn where they belong, wherever they're written,
but at the card's level: a form or cascading value they're written inside doesn't reach them. Put a
form's buttons in a `CardFooter` inside the form, or the form around the whole card.

`Dividers` on the card makes each `CardBody` a section, with a line across the card between
sections, as in a settings page. Content outside a `CardBody` isn't a section.

```razor
<Card Title="Delete project?" Description="This can't be undone.">
    <CardActions><IconButton Icon="Lucide.Ellipsis" Label="More" Variant="ButtonVariant.Ghost" /></CardActions>
    <CardBody Gap="2">...</CardBody>
    <CardFooter Justify="Justify.End">
        <Button Variant="ButtonVariant.Ghost">Cancel</Button>
        <Button Accent="Accent.Danger">Delete</Button>
    </CardFooter>
</Card>
```

## Stat

A key figure: `Label`, `Value` (or content), and optionally `Change` with a `Trend` (`Up`, `Down`,
`Flat`) and a muted `Description` such as "vs last month". It's a `<dl>`, so the label names the
value for screen readers. Up is shown in the success color and down in danger; set `ChangeStatus`
when down is good, such as for costs or response times. The arrow is hidden from screen readers and
`TrendLabel` ("Up", "Down", "No change") is read in its place. `Icon` adds a tinted square at the
end; `Size` scales the text. Put stats in a `Grid`, in `Card`s or not.

```razor
<Stat Label="Revenue" Value="$48,210" Change="12%" Trend="StatTrend.Up" Description="vs last month" />
```

## DescriptionList

Terms and their details, such as an order's fields: `DescriptionItem Term="Status"` (or
`TermContent`) with the detail as content, rendered as a `<dl>` with each pair in a `<div>`.
`Orientation.Horizontal` (the default) puts terms beside details from 640px up, the term column as
wide as the longest term (up to 40%); phones get them stacked. `Orientation.Vertical` puts each term
above its detail, and `Columns` (1 to 4) flows the items into columns from 640px up. `Divided` adds
lines between items; `Size` sets the text size and spacing.

## EmptyState

What to show when there's nothing yet: `Icon` (in a tinted circle), `Title` (`TitleLevel`, default
3), `Description`, any content, and `EmptyStateActions` such as a "New invoice" button or "Clear filters".
`Bordered` draws a dashed outline for an empty area inside a page (`BorderStyle` and `BorderColor`
change it); `Size` scales it.

## Image and VideoEmbed

`Image` loads lazily (`Lazy`, on by default; turn it off for the first image on a page) and keeps
its space while loading: give `IntrinsicWidth` and `IntrinsicHeight` (the file's own size), or a
`Ratio` such as `16 / 9.0`, which the image fills (`Fit` `Cover`, the default) or fits inside
(`Contain`). `Alt` is required; use `""` for a decorative image. `Caption` wraps it in a `figure`. It
takes the style props, such as `Radius`.

`VideoEmbed` shows a YouTube video without loading YouTube until someone plays it: a poster with a
play button, which is a link to the video on YouTube. With script, clicking swaps in the player from
`youtube-nocookie.com`, in place. `Label` names the player and the button. The poster defaults to
YouTube's thumbnail; set `Poster` to your own image to load nothing from YouTube before play.

For a video that isn't out yet, leave out `YouTubeId`: the box keeps its place with your `Poster`
(dimmed, if set) and "Coming soon" in the middle (`EmptyText`, or `EmptyContent` for your own markup),
and nothing loads from YouTube. Add the id when it's published.

```razor
<Image Src="/shots/console.png" Alt="The rules list" Ratio="16 / 9.0" Radius="Radius.Lg" Caption="The console" />
<VideoEmbed YouTubeId="dQw4w9WgXcQ" Label="Building a form" />
<VideoEmbed Label="Deploying to Azure" Poster="/posters/deploy.jpg" EmptyText="Out Friday" />
```

## Timeline

A vertical list of events, such as a commit log, an order's history or a changelog: an `ol` of
`TimelineItem`s, each with a marker on a rail, a `Title` (a link with `Href`), a `Time` (a `time`
element when `DateTime` is given) and any content under it, such as a commit link or actions.
`Icon` puts an icon in the marker, `Status` colors it, and `Current` marks where things stand now:
the marker lights in the accent and the item gets `aria-current`. Markers and the rail sit on whole
pixels; markers take `--ib-timeline-bg` (the surface by default) so the rail passes behind them.

```razor
<Timeline>
    <TimelineItem Title="Add Section" Time="Oct 13" DateTime="2026-10-13" Current>
        <Anchor Href="https://github.com/...">93c85d4</Anchor>
    </TimelineItem>
    <TimelineItem Title="List and Prose" Time="Oct 13" Status="Status.Success" />
</Timeline>
```

## Kbd

A key, `<Kbd>Esc</Kbd>`, or a combination, `<Kbd Keys="Ctrl+K" />`: nested `<kbd>` elements, one
box per key, joined by `Separator` ("+"). Spaces around "+" are ignored, and "Ctrl++" is Ctrl and the
plus key. Sized in em, so it fits the text around it.

## Table

Sorts and pages through the URL: headers of `Sortable` columns and page numbers are links that set
`?sort=` (`-total` for descending) and `?page=`. So tables work with no script, survive reloads and
can be shared. On interactive pages the links are ordinary navigations and the table follows the
URL. With enhanced navigation that's no full reload, but the server renders the page again; only an
app with a client-side router (full interactivity) stays in the browser.

```razor
<Table Items="orders" PageSize="10" Caption="Orders">
    <PropertyColumn Property="o => o.Customer" Sortable />
    <PropertyColumn Property="o => o.Total" Format="C" Sortable />
    <TemplateColumn Title="Status"><Badge>@context.Status</Badge></TemplateColumn>
</Table>
```

A column's key in the URL is its `SortKey`, the property name in lower case by default. `Caption`
names the table for screen readers (visually hidden).

**Column filters.** `Filterable` on a column adds a filter. Where it goes is `FilterPlacement`:

- **`Header` (the default):** a funnel button right after the column's title and sort icon,
  tinted while the column is filtered (end-aligned columns, such as numbers, mirror the group). It opens the filter in a popover (a menu of values for enums and booleans), and the
  active filters show as removable tags above the table, with Clear. Popovers open natively, so
  this works with JavaScript off: Enter or Apply in the popover submits.
- **`Row`:** a row of fields under the headers, always in view, for tables people filter
  constantly. With JavaScript off, Enter or the Apply button below the table submits.

A table inside another form (a `Selectable` table in a selection form, say) still filters with
JavaScript on. With it off, browsers ignore a form nested in a form, so typed filters (text,
numbers, dates) can't submit there; the enum and boolean menus are links and still work.

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
is interactive, still use the URL (the prerender without the filters' Apply row, which the
interactive table doesn't have).

- `PropertyColumn` takes its title from the property (or `[Display(Name)]`), right-aligns numbers and
  formats values (`Format="C"`).
- `TemplateColumn` shows anything and sorts with `SortBy` (with a `SortKey`).
- Every column takes `Title`, `Align`, `Width` and `Class`.
- Pass `Items` (a list, or an EF Core `IQueryable`, sorted and paged by the database) or an
  `ItemsProvider` that loads each page (`request.Apply(query)`).
- Also: `PageSize`, `InitialSort`, `QueryPrefix` for several tables on a page, `MaxHeight` for a
  sticky header, `Striped`, `Size`, `RowClass`, `EmptyText` (or `EmptyContent`). Without a
  `PageSize` or `PageSizeOptions` every item shows on one page.
- Page size: `PageSizeOptions="new[] { 10, 25, 50 }"` adds a "Rows per page" menu to the footer.
  Each size is a link (`?size=25`, only listed sizes are accepted from the URL), so it works with
  no script, and the new page is the one holding the first row you were looking at.
  `@bind-PageSize` (an `int?`) keeps it in the component on interactive pages.
- Lines: `RowLines` (on by default), `ColumnLines` (off by default) and `Bordered`, the border
  around the table (on by default; turn it off for a table inside a card).

**Selection.** `Selectable` adds a checkbox to each row, valued with the row's `ItemKey`.

- **No JavaScript:** inside a form, the checked rows post their keys under `SelectionName`
  (`"selected"` by default, after any `QueryPrefix`); read them with `[SupplyParameterFromForm]`. Checked rows are tinted.
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

Fields: `FilterSearch` (`?q=`), `FilterSelect` (`Items`, or an enum with `EnumType`, with an "All"
option first) and `FilterCheckbox` (`name=true` when checked). Each takes a `Name` (its query key)
and a `Label`, kept for screen readers but not shown. List your own inputs' names in `Fields`.
`Live="false"` keeps an Apply button instead of applying as you type.

```razor
<FilterForm>
    <FilterSearch Placeholder="Search customers" />
    <FilterSelect Name="status" Label="Status" EnumType="typeof(OrderStatus)" />
</FilterForm>
```

## ColorSchemeSwitch

Lets the user pick System, Light or Dark. Three looks:

- `Menu` (the default): a ghost icon button showing the page's scheme (sun or moon), opening a menu
  of System, Light and Dark with the current one checked. For headers.
- `Toggle`: one icon button that flips between light and dark (no System).
- `Segmented`: all three side by side, with labels or `IconOnly`, for a settings page.

```razor
<ColorSchemeSwitch />
<ColorSchemeSwitch Variant="ColorSchemeSwitchVariant.Toggle" />
<ColorSchemeSwitch Variant="ColorSchemeSwitchVariant.Segmented" IconOnly />
<ColorSchemeSwitch @bind-Value="scheme" />   @* interactive pages *@
```

The icon is worked out in CSS from the page's actual scheme, so it's right even when the device
decides. `Size` matches the buttons around it. `@bind-Value` is a `ColorScheme` (`Auto` for System,
`Light`, `Dark`).

The choice is kept in the `ib-scheme` cookie. Put `IzzyTheme` in `<head>` so the server applies it
before the first paint. (`IzzyTheme` also declares the app's preset, brand color, fonts and default
`ColorScheme`; see [Styling](styling.md).) With JavaScript the switch is instant and other open tabs follow; with it
off, each option is a link to `/_izzy/scheme`, which `app.MapIzzyBlazor()` adds: it saves the
cookie and redirects back to the page.

## Scrollbars

IzzyBlazor's scrolling areas (the sidebar, tables, menus, dialogs, code blocks and so on) have thin,
theme-colored scrollbars with no arrow buttons, a little stronger while the pointer is on the
thumb. `--ib-color-scrollbar` and `--ib-color-scrollbar-hover` set the colors; high-contrast mode
uses system colors.
For a scroll region of your own (a chat log, a long list in a card), add `ib-scroll` to the element
that scrolls.

`Scrollbars` on `IzzyTheme` chooses whose scrollbars the app uses:

- `ThemeScrollbars.Auto` (the default): the page keeps the system's own scrollbar, and the library's
  scroll areas get the thin themed ones. The page's scrollbar then behaves as on any other site:
  on macOS, and in browsers with overlay scrollbars (Windows 11), it fades when you stop scrolling
  and takes no room.
- `ThemeScrollbars.Themed`: the page's scrollbar gets the thin look too, matching the rest. Styled
  scrollbars never fade, so it stays visible, on macOS as well. `ib-scrollbars` on `<html>` does
  the same without an `IzzyTheme`.
- `ThemeScrollbars.Native`: the system's scrollbars everywhere, including the library's scroll
  areas. `ib-scrollbars-native` on `<html>` does the same without an `IzzyTheme`.

```razor
<IzzyTheme Scrollbars="ThemeScrollbars.Native" />
```

On a page that `AppShell` scrolls, opening the sidebar drawer or panel, or a dialog, stops the page
scrolling and takes its scrollbar away. So that the page doesn't widen and shift sideways,
IzzyBlazor's script pads it by the scrollbar's width while it's locked, and a dialog, drawer or
panel and its backdrop reach the window's edge. A page too short to scroll has no scrollbar and
no padding. With JavaScript off, room for the scrollbar is kept instead, even when the page can't
scroll (`scrollbar-gutter: stable`): with classic scrollbars a short page shows an empty strip
where the scrollbar would be, and overlays stop short of it. Overlay scrollbars take no room, so
there neither shows.

## Text and other languages

Components name their buttons and announce states in English by default: "Close", "Loading",
"Next page", "No matches". Most of these are also a parameter on the component (`CloseLabel`,
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

`AccordionItem`, `Collapsible`, `Menu`, `Popover`, `Dialog` and `Drawer` take `Open` (the starting
state) and `@bind-Open`. The browser always does the opening and closing; on
interactive pages an `ibtoggle` event reports changes back, and setting `Open` from C# opens or closes
the element.
