# IzzyBlazor templates

`dotnet new` templates for [IzzyBlazor](https://izzyblazor.com), the Blazor component library built for static server-side rendering first.

`izzy-blazor` is Microsoft's Blazor Web App (`dotnet new blazor`) rebuilt with IzzyBlazor's components and theme. It has the same options, names and defaults, in every render mode. With `--auth Individual`, ASP.NET Core Identity's account pages keep Microsoft's code and are drawn with IzzyBlazor.

## Install

```bash
dotnet new install IzzyBlazor.Templates
```

## Create an app

```bash
dotnet new izzy-blazor -n MyApp
```

Microsoft's options work as they do in `dotnet new blazor`:

| Option | What it does |
| --- | --- |
| `--interactivity None\|Server\|WebAssembly\|Auto` | The interactive render mode. `None` makes every page static. Default `Server`. |
| `--all-interactive` | Makes every page interactive. Otherwise pages are static, and you mark the ones that need it. |
| `--auth None\|Individual` | `Individual` adds accounts stored in the app's database, with Identity's pages. |
| `--use-local-db` | With `Individual`, uses SQL Server LocalDB instead of SQLite. |
| `--empty` | Leaves out the sample pages and the sidebar. |

IzzyBlazor adds:

| Option | What it does |
| --- | --- |
| `--preset Standard\|Compact\|Soft\|Editorial` | The theme preset. Compact suits data and admin apps, Soft consumer apps, Editorial reading and docs. |
| `--brand <color>` | The brand color: `blue`, `ink`, `indigo`, `violet`, `teal`, `green`, `rose`, or a hex color such as `#0a7d5a`. |
| `--nav Sidebar\|Top` | Navigation in a sidebar, or across the top bar. |
| `--content-width Auto\|Full\|64\|72\|80` | How wide pages get, in rem. |
| `--shell Tinted\|Inset\|Flat\|Canvas` | How the app shell looks. |
| `--mcp` | Connects AI assistants (Claude Code, VS Code, Visual Studio, Cursor) to IzzyBlazor's docs server. |

Run `dotnet new izzy-blazor --help` for every option.

## Documentation

- [Get started](https://izzyblazor.com/docs/get-started)
- [Components](https://izzyblazor.com/docs/components)
- [Source and issues](https://github.com/enabledevmode/izzyblazor)

## License

MIT. `izzy-blazor` is ported from Microsoft's Blazor Web App template (MIT, dotnet/aspnetcore); see THIRD-PARTY-NOTICES.md in the package.

IzzyBlazor is an independent project and is not affiliated with Microsoft.
