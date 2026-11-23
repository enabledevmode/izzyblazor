# IzzyBlazor CLI

The `izzy` command makes new IzzyBlazor apps and adds pages and blocks to the app you have, from IzzyBlazor's catalog.

```sh
dotnet tool install -g IzzyBlazor.Cli --prerelease
izzy new starter blazor -n MyApp
cd MyApp
izzy add crud Product
```

- `izzy add crud <Model>` reads a model class from your app's source and writes a page whose grid adds, edits and deletes its records.
- `izzy init` sets up IzzyBlazor in an app you have: a Blazor Web App, or an MVC or Razor Pages app, which gets a Blazor root beside its pages. It builds the app afterwards and says how to fix a name clash.
- `izzy list` shows the catalog; `izzy describe <item>` lists an item's settings.
- `izzy login` signs this machine in to a free IzzyBlazor account, which the app starters, the blocks and the CRM kit come with. In CI, set `IZZY_KEY` to a key from your account page.
- `--dry-run` writes nothing, `--diff` shows the changes as a unified diff, and `--json` prints them as data for editors and agents.
- Files that exist with other content are never replaced without `--overwrite`.
- Each item added is recorded in `.izzy/items.json` with its version and settings.

Docs: https://izzyblazor.com/docs/cli
