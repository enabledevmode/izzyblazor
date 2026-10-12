// One command for day-to-day development:  npm run dev
//
// Runs the asset watcher (CSS + TypeScript) and `dotnet watch` on the test host
// side by side. Edit a component, its CSS or a custom element and the browser
// updates. Stop both with Ctrl+C.

import { spawn } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const children = [];

function run(name, command, args, cwd) {
  const child = spawn(command, args, { cwd, stdio: "inherit", shell: process.platform === "win32" });
  child.on("exit", (code) => {
    console.log(`[${name}] exited with code ${code}`);
    shutdown();
  });
  children.push(child);
}

function shutdown() {
  for (const child of children) if (!child.killed) child.kill();
  process.exit();
}

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);

run("assets", "node", ["build/build-assets.mjs", "--watch"], root);
// The watcher owns the assets, so MSBuild skips its own asset step.
run("host", "dotnet", ["watch", "--project", "tests/IzzyBlazor.TestHost", "--property:SkipAssetBuild=true"], root);
