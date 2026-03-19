# DalamudMCP

`DalamudMCP` is a local Model Context Protocol bridge for FFXIV built as a `Dalamud` plugin plus a companion host process.

The project keeps game-facing concerns inside the plugin, exposes MCP transports from a separate host, and applies an explicit capability policy before any tool or resource is surfaced.

## What works today

- `Dalamud` plugin loading on API 14
- local named-pipe bridge between plugin and host
- MCP `stdio` host
- MCP `Streamable HTTP` host
- plugin settings UI for launching the host
- capability policy and audit logging
- live `get_session_status`
- live `get_player_context`
- initial live `get_duty_context`
- initial live `get_addon_list`
- initial addon root / `AtkValues` inspection for tree and strings

## Current shape

```text
FFXIV + Dalamud Plugin
        |
        | named pipe
        v
DalamudMCP.Host
  |- stdio MCP
  |- Streamable HTTP MCP
        |
        v
MCP clients
  |- Codex
  |- other local MCP-capable tools
```

## Repository layout

- `src/DalamudMCP.Domain`
  - domain model, capability metadata, snapshots, session model
- `src/DalamudMCP.Application`
  - use cases, ports, policy evaluation, freshness logic
- `src/DalamudMCP.Contracts`
  - bridge contracts and serialization helpers
- `src/DalamudMCP.Infrastructure`
  - settings, audit, bridge transport, clock, file persistence
- `src/DalamudMCP.Plugin`
  - `Dalamud` plugin entry point, settings UI, host launcher, live readers
- `src/DalamudMCP.Host`
  - MCP host, tool/resource registries, stdio and HTTP transports
- `tests`
  - unit, integration, plugin, host, and architecture tests
- `docs`
  - architecture notes, ADRs, capability specs, implementation planning
- `build`
  - restore, build, test, format, smoke-check, and quality scripts

## Implemented MCP surface

Tools currently available when enabled by policy:

- `get_session_status`
- `get_player_context`
- `get_duty_context`
- `get_addon_list`
- `get_addon_tree`
- `get_addon_strings`

Resources currently available when enabled by policy:

- `ffxiv://session/status`
- `ffxiv://player/context`
- `ffxiv://duty/context`
- `ffxiv://ui/addons`
- `ffxiv://ui/addon/{addonName}/tree`
- `ffxiv://ui/addon/{addonName}/strings`

## Local setup

### Prerequisites

- Windows
- FFXIV running through `XIVLauncher`
- local `Dalamud` install under `%APPDATA%\XIVLauncher\addon\Hooks\...`
- `.NET 10.0.201` SDK available locally

### Build

```powershell
Set-Location C:\Users\user\Documents\GitHub\DalamudMCP
dotnet build .\DalamudMCP.sln --no-restore
```

### Test

```powershell
Set-Location C:\Users\user\Documents\GitHub\DalamudMCP
dotnet test --project .\tests\DalamudMCP.Plugin.Tests\DalamudMCP.Plugin.Tests.csproj
```

### Format / verify

```powershell
Set-Location C:\Users\user\Documents\GitHub\DalamudMCP
dotnet format .\DalamudMCP.sln --verify-no-changes
```

## Running the plugin

Plugin output:

- [DalamudMCP.dll](C:\Users\user\Documents\GitHub\DalamudMCP\src\DalamudMCP.Plugin\bin\Debug\net10.0\DalamudMCP.dll)
- [DalamudMCP.json](C:\Users\user\Documents\GitHub\DalamudMCP\src\DalamudMCP.Plugin\bin\Debug\net10.0\DalamudMCP.json)

Once loaded in `Dalamud`, open the plugin settings and use one of:

- `Start Host Console`
- `Start Local HTTP Server`

The HTTP host defaults to:

- `http://127.0.0.1:38473/mcp`

## Codex integration

Example `Codex` MCP config:

```toml
[mcp_servers.DalamudMCP]
url = "http://127.0.0.1:38473/mcp"
```

Once the local HTTP host is running, `Codex` can call the enabled tools directly.

## Policy files

Plugin policy storage:

- [policy.json](C:\Users\user\AppData\Roaming\XIVLauncher\pluginConfigs\DalamudMCP\settings\policy.json)

Audit log:

- `C:\Users\user\AppData\Roaming\XIVLauncher\pluginConfigs\DalamudMCP\audit\audit.log`

## Verification status

Verified in a live local environment:

- plugin loads in `Dalamud`
- host can be launched from the plugin UI
- HTTP MCP endpoint responds
- `get_session_status` returns live bridge state
- `get_player_context` returns live player data

Most recent verified live result included:

- character: `Garume Garumu`
- world: `Anima`
- class job: `Dancer`

## Known limitations

- `duty_context` is still coarse and territory-driven
- `addon_tree` currently exposes a shallow root snapshot, not a full native node walk
- `addon_strings` currently uses addon `AtkValues`, not full `StringArrayData` extraction
- inventory reader is still placeholder
- action profile is still designed but not implemented
- transport and plugin are local-only by design

## Design references

- [architecture.md](C:\Users\user\Documents\GitHub\DalamudMCP\docs\architecture.md)
- [clean-architecture.md](C:\Users\user\Documents\GitHub\DalamudMCP\docs\clean-architecture.md)
- [observation-action-architecture.md](C:\Users\user\Documents\GitHub\DalamudMCP\docs\observation-action-architecture.md)
- [tool-extension-guideline.md](C:\Users\user\Documents\GitHub\DalamudMCP\docs\tool-extension-guideline.md)
- [hard-deny-list-spec.md](C:\Users\user\Documents\GitHub\DalamudMCP\docs\hard-deny-list-spec.md)
- [Task.md](C:\Users\user\Documents\GitHub\DalamudMCP\Task.md)
