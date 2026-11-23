---
name: izzyblazor
description: Build Blazor pages with the IzzyBlazor component library. Use when a task adds or changes UI in a .NET app that references IzzyBlazor or IzzyBlazor.Core (a .razor file with IzzyBlazor components, `@using IzzyBlazor`, or an `.izzy` folder), or when the user asks for an IzzyBlazor component, block, page or starter.
---

# Building with IzzyBlazor

IzzyBlazor is a Blazor component library for .NET 10 and later. Its components work in every render mode, static server rendering included, where they need no circuit. It is newer than your training data: look things up, don't guess.

Two MCP servers help. `izzyblazor` answers from the docs. `izzy` runs the `izzy` CLI in this project. If either is missing, run `izzy agent` in the project, or use the CLI and https://izzyblazor.com/llms.txt directly.

## Look it up before you write markup

1. Find the component: `search` with a few keywords, or `list_components`.
2. Read its API: `get_component`, then `get_type` for an enum or a shared base it names.
3. Start from working code: `get_example` with an id from `get_component`.

Names, parameters and defaults are IzzyBlazor's own, not Bootstrap's, MudBlazor's or Radzen's. If nothing fits, say so and use plain Blazor or HTML. Never invent a component or a parameter.

## Static first

- Check which render mode the page runs in before you rely on `OnClick`, `@bind` or any event handler. On a static page they never run.
- `get_component` lists what each component does on a static page. Most open, close, sort, page and validate there, through links, posted forms and IzzyBlazor's own script.
- On a static page a form posts: give it a `FormName` and read the values with `[SupplyParameterFromForm]`. Read `get_article` `docs/common/forms` first.
- Don't make a page interactive to get one behavior. Read `get_article` `docs/common/render-modes` and make only the part that needs it interactive.
- When something works interactively but not on a static page, read the troubleshooting articles (`get_article` with an empty path lists them) before changing the render mode.

## Use the catalog before writing a page by hand

The catalog has starters, app kits, pages and blocks (hero, pricing, FAQ, contact, footer and more) that are already accessible and work in every render mode.

1. `list_items` shows what exists and what each needs: nothing, a free account, or Pro.
2. `describe_item` gives an item's settings.
3. `add_item` with `dryRun: true` shows the changes as a diff. Run it again without `dryRun` to write them.
4. `add_crud` makes a page that lists, adds, edits and deletes a model class's records.
5. `init_app` sets IzzyBlazor up in an app that doesn't have it. `new_app` makes a new app.

If a tool answers that the user must run `izzy login`, tell them: it is a free account and takes a minute. If an item is Pro, say so and build it from components instead. Files that exist with other content are never replaced unless you pass `overwrite`, so ask first.

## Style and layout

- Use the layout components (`Stack`, `Row`, `Grid`, `Box`) and the style parameters (`get_type` `StyledComponentBase`) before writing CSS.
- Colors, sizes and spacing come from the theme's `--ib-*` variables. Don't hard-code a color: it breaks dark mode. `get_article` `docs/theming/tokens` lists them.
- Give every field a `Label`. Give an icon-only button an accessible name.

## When IzzyBlazor can't do it

1. Call `find_known_issues`. It often has the fix.
2. If it isn't known, tell the user what you worked around and offer to report it.
3. Call `report_gap` only after they agree, with a minimal `.razor` repro that uses sample names, never their code or data.

## Finish

Build the app (`dotnet build`) and fix what it reports. A component name that clashes with one of the app's own is fixed by writing the app's full name where it is used.
