# IzzyBlazor CLI

The `izzy` command makes new IzzyBlazor apps and adds blocks and pages to the app you have, from IzzyBlazor's catalogue.

```sh
dotnet tool install -g IzzyBlazor.Cli
izzy new izzy-blazor -n MyApp
cd MyApp
izzy add hero --layout Split
```

- `izzy list` shows the catalogue; `izzy describe <item>` lists an item's settings.
- `--dry-run` writes nothing, `--diff` shows the changes as a unified diff, and `--json` prints them as data for editors and agents.
- Files that exist with other content are never replaced without `--overwrite`.
- Each item added is recorded in `.izzy/items.json` with its version and settings.

Docs: https://izzyblazor.com
