# DalamudMCP

`DalamudMCP` exposes live FFXIV state from a Dalamud plugin to both a local CLI and MCP clients.

The current codebase is built around:

- one handwritten operation file per tool
- the same operation definition drives both CLI and MCP
- the plugin owns live game execution
- the CLI and MCP layers talk to the plugin over a local named pipe

## What You Get

- direct CLI commands such as `player context` and `inventory summary`
- MCP over `stdio` with `serve mcp`
- MCP over local HTTP with `serve http`
- plugin auto-discovery so normal CLI usage does not need a manual pipe name
- a plugin settings window that shows runtime health and can start the bundled HTTP server

## Safety Defaults

`DalamudMCP` is observation-first.

- observation tools are enabled by default
- action tools are default-off until you enable them in the plugin UI
- unsafe integration tools are default-off and hidden behind a separate developer toggle

This is intentional. The public surface should stay conservative unless you explicitly widen it.

## Current Tool Surface

Observation tools:

- `session.status`
- `player.context`
- `duty.context`
- `inventory.summary`
- `addon.list`
- `addon.tree`
- `addon.strings`
- `fate.context`
- `nearby.interactables`
- `quest.status`
- `quest.available`
- `quest.current-objective`
- `game.screenshot`

Action tools:

- `target.object`
- `interact.with.target`
- `move.to.entity`
- `move.to.nearby.interactable`
- `teleport.to.aetheryte`
- `duty.action`
- `addon.input`
- `addon.event`
- `addon.callback.values`
- `addon.select.menu-item`

Developer-only unsafe tool:

- `unsafe.invoke.plugin-ipc`

The handwritten truth source for these operations lives under [`src/DalamudMCP.Plugin/Operations`](./src/DalamudMCP.Plugin/Operations).

## Install And Run

### 1. Build

```powershell
.\build\restore.ps1
.\build\build.ps1 -NoRestore
```

If Dalamud is not installed in the default developer path, set `DALAMUD_HOME` or pass `-DalamudHome` to the build scripts.

```powershell
$env:DALAMUD_HOME = 'C:\path\to\Hooks\dev'
.\build\build.ps1 -NoRestore -DalamudHome $env:DALAMUD_HOME
```

### 2. Install The Plugin

The active plugin is:

- [`src/DalamudMCP.Plugin`](./src/DalamudMCP.Plugin)

Build output:

- [`src/DalamudMCP.Plugin/bin/Debug/net10.0/DalamudMCP.dll`](./src/DalamudMCP.Plugin/bin/Debug/net10.0/DalamudMCP.dll)
- [`src/DalamudMCP.Plugin/bin/Debug/net10.0/DalamudMCP.json`](./src/DalamudMCP.Plugin/bin/Debug/net10.0/DalamudMCP.json)

Load that plugin in Dalamud, then open its configuration window.

### 3. Try The CLI

```powershell
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- player context
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- session status --json
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- inventory summary
```

Normal CLI use auto-discovers the active plugin instance from `active-instance.json`.

`--pipe <name>` is still supported as an advanced override for debugging or multi-instance scenarios.

### 4. Run MCP

`stdio` MCP:

```powershell
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- serve mcp
```

Local HTTP MCP:

```powershell
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- serve http --port 38473
```

Default endpoint:

- `http://127.0.0.1:38473/mcp`

The plugin UI can also start and stop the bundled HTTP MCP server for you.

### 5. Build A Release Package

Build the plugin in `Release` and package the plugin output directory manually.

```powershell
.\.dotnet\dotnet.exe build .\src\DalamudMCP.Plugin\DalamudMCP.Plugin.csproj -c Release
```

If you need a custom Dalamud path:

```powershell
$env:DALAMUD_HOME = 'C:\path\to\Hooks\dev'
.\.dotnet\dotnet.exe build .\src\DalamudMCP.Plugin\DalamudMCP.Plugin.csproj -c Release
```

The packaged output is written under `src/DalamudMCP.Plugin/bin/Release/DalamudMCP/`. Upload the generated `latest.zip` manually when cutting a release.

## Optional Integrations

When compatible plugins are installed, `DalamudMCP` can use them behind existing high-level tools.

- `teleport.to.aetheryte` can fall back to `Lifestream` for local aethernet travel
- movement operations can use `vnavmesh` when available

These integrations stay behind normal high-level tools by default. Raw integration escape hatches remain developer-only.

## Build And Test

GitHub Actions CI validates the portable layers and repository hygiene.
The plugin build remains a local or self-hosted Windows step because `Dalamud.NET.Sdk` still needs a resolved `DALAMUD_HOME` / Hooks dev directory.

Build:

```powershell
.\build\build.ps1 -NoRestore
```

Full test run:

```powershell
.\build\test.ps1 -NoBuild
```

The active solution is [`DalamudMCP.slnx`](./DalamudMCP.slnx).

## Repository Layout

Active projects:

- [`src/DalamudMCP.Framework`](./src/DalamudMCP.Framework)
- [`src/DalamudMCP.Framework.Cli`](./src/DalamudMCP.Framework.Cli)
- [`src/DalamudMCP.Framework.Generators`](./src/DalamudMCP.Framework.Generators)
- [`src/DalamudMCP.Framework.Mcp`](./src/DalamudMCP.Framework.Mcp)
- [`src/DalamudMCP.Protocol`](./src/DalamudMCP.Protocol)
- [`src/DalamudMCP.Cli`](./src/DalamudMCP.Cli)
- [`src/DalamudMCP.Plugin`](./src/DalamudMCP.Plugin)
