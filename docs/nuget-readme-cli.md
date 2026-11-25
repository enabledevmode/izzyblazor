# IzzyBlazor CLI

The `izzy` command makes new IzzyBlazor apps and adds pages and blocks to the app you have, from IzzyBlazor's catalog.

```sh
dotnet tool install -g IzzyBlazor.Cli --prerelease
izzy new starter blazor -n MyApp
cd MyApp
dotnet izzy add crud Product
```

An app izzy creates or sets up has its own copy of izzy, pinned in `.config/dotnet-tools.json` to the version of its IzzyBlazor package. In the app's folder, run `dotnet tool restore` once, then `dotnet izzy <command>`: it needs nothing on your PATH.

If the terminal answers "command not found" after the global install, the folder .NET installs tools in isn't on your PATH. Add it, then open a new terminal:

```sh
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.zprofile        # zsh (macOS)
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.bash_profile    # bash
```

- `izzy add crud <Model>` reads a model class from your app's source and writes a page whose grid adds, edits and deletes its records.
- `izzy init` sets up IzzyBlazor in an app you have: a Blazor Web App, or an MVC or Razor Pages app, which gets a Blazor root beside its pages. It builds the app afterwards and says how to fix a name clash.
- `izzy update` moves the app to the newest IzzyBlazor: the package in its project files and the izzy in its tool manifest. Outside an app, or with `--global`, it updates the izzy on your machine. izzy says in one line when a newer release is out, and never updates by itself.
- `izzy upgrade` finds where the app uses what the releases between its old version and the new one removed or renamed, in its `.razor` and `.cs` files, and prints a plan: each rename it would make, and the rest by file and line. `izzy upgrade --apply` makes the renames (Pro). `izzy update` lists the changes themselves.
- `izzy list` shows the catalog; `izzy describe block hero` lists an item's settings.
- `izzy login` signs this machine in to a free IzzyBlazor account, which the app starters, the blocks and the CRM kit come with. In CI, set `IZZY_KEY` to a key from your account page.
- `izzy audit <url>` checks a page's accessibility with axe-core in the Chrome or Edge on your machine, and writes a report and the results as JSON. `--run` starts your app for it.
- `izzy agent` sets up your coding agent in the app. It writes a skill that teaches the agent to build with IzzyBlazor, and connects the docs server and `izzy mcp`, which runs izzy's commands as tools.
- `--dry-run` writes nothing, `--diff` shows the changes as a unified diff, and `--json` prints them as data for editors and agents.
- Files that exist with other content are never replaced without `--overwrite`.
- Each item added is recorded in `.izzy/items.json` with its version and settings.

Docs: https://izzyblazor.com/docs/cli
