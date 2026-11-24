# {{ProjectName}}

<!--#if (ShellStarter) -->
A Blazor Web App made with IzzyBlazor's {{Starter}} starter.
<!--#else -->
A Blazor Web App made with the IzzyBlazor App starter.
<!--#endif -->

## Run it

<!--#if (UseWebAssembly) -->
```
dotnet run --project {{ProjectName}}
```

<!--#if (InteractiveAtRoot) -->
The solution has two projects: `{{ProjectName}}` is the server, and `{{ProjectName}}.Client` holds what runs in the browser. Every page is interactive, so the layout and the pages are in `{{ProjectName}}.Client`; the other paths below are in `{{ProjectName}}`.
<!--#else -->
The solution has two projects: `{{ProjectName}}` is the server, and `{{ProjectName}}.Client` holds the components that run in the browser. The paths below are in `{{ProjectName}}`.
<!--#endif -->
<!--#else -->
```
dotnet run
```
<!--#endif -->

Open the address it prints. The home page shows how the app was set up and your theme in a few components.

## How pages render

<!--#if (Interactivity == "None") -->
Every page is static: the server draws it, and the browser needs no circuit and no WebAssembly download. IzzyBlazor's components still work on a static page. Forms post, and a small script runs the menus, dialogs and other moving parts.
<!--#elif (InteractiveAtRoot) -->
Every page is interactive ({{Interactivity}}), set once on `Routes` in `Components/App.razor`.
<!--#else -->
Pages are static unless they ask to be interactive with `@rendermode` ({{Interactivity}}). IzzyBlazor's components work either way, so add a render mode only where a page needs C# to run on each click.
<!--#endif -->

[Render modes](https://izzyblazor.com/docs/common/render-modes) explains the choice.

## Where things are

| What | File |
| --- | --- |
| The shell: header, navigation and where pages go | `Components/Layout/MainLayout.razor` |
| Pages | `Components/Pages/` |
| The theme (preset and brand color) | `IzzyTheme` in `Components/App.razor` |
| Your own styles | `wwwroot/app.css` |
| Services and settings | `Program.cs`, `appsettings.json` |
<!--#if (IndividualLocalAuth) -->
| Sign-in pages | `Components/Account/` |
| Accounts and the database | `Data/` |
<!--#endif -->

To add a page, add a `.razor` file with an `@page` line to `Components/Pages/`, then a `NavItem` for it in `MainLayout.razor`.

<!--#if (IndividualLocalAuth) -->
## Sign-in

The app has its own accounts, kept by ASP.NET Core Identity.

<!--#if (Registration) -->
- **Make an account:** run the app and choose Register. Nobody has an account until someone registers.
- **Confirming an email address:** the app can't send email yet, so the page after Register shows the confirmation link itself. To send real email, replace `IdentityNoOpEmailSender` in `Program.cs` with a sender of your own.
<!--#else -->
- **Accounts:** there is no Register page, so people can't make their own. Create accounts in your own code with `UserManager<ApplicationUser>`.
<!--#endif -->
<!--#if (UseLocalDB) -->
- **The database:** SQL Server LocalDB, named in `appsettings.json`. Create it with `dotnet ef database update` before the first sign-in.
<!--#else -->
- **The database:** SQLite, in the file `Data/app.db`. It starts with no accounts.
<!--#endif -->
- **Pages only for signed-in people:** add `@attribute [Authorize]` to the page.

<!--#endif -->
## Add to it

The `izzy` command adds finished parts to this app (`dotnet tool install -g IzzyBlazor.Cli --prerelease`).

```
izzy list                 # the blocks, starters and kits you can add
izzy add block hero       # a page section, placed on a page for you
izzy add crud Product     # a page that lists, adds, edits and deletes a model class
```

Add `--dry-run` to see what a command would change before it writes anything. Some items come with a free account: `izzy login` signs this machine in once.

<!--#if (Mcp) -->
## Coding agents

`.mcp.json` connects your coding agent to IzzyBlazor's docs server, so it can look up components and examples while it works. `izzy agent` also adds a skill that teaches it to build with IzzyBlazor.

<!--#endif -->
## Learn more

- [Components](https://izzyblazor.com/docs/components): every component, with examples in each render mode
- [Docs](https://izzyblazor.com/docs): theming, forms, data and layout
- [Templates](https://izzyblazor.com/templates) and [Blocks](https://izzyblazor.com/blocks)

The code in this folder is yours to change and ship. [License](https://izzyblazor.com/docs/cli/license) has the details.
