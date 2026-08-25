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
- MCP over bearer-authenticated local HTTP with `serve http`
- plugin auto-discovery so normal CLI usage does not need a manual pipe name
- a plugin settings window that shows runtime health and can start the bundled HTTP server
- generic game action execution, Lumina Excel exploration, and plugin lifecycle/package management

## Safety Defaults

`DalamudMCP` is observation-first, but it can expose broad mutation capabilities when the user enables them.

- observation tools are enabled by default
- action tools are default-off until you enable them in the plugin UI
- unsafe integration tools are default-off and hidden behind a separate developer toggle
- an explicit capability policy overrides the legacy toggles and is enforced again for every invocation
- confirm-once policies show the exact arguments in the plugin UI and consume approval after one matching retry
- policies can constrain plugin names, action IDs, jobs, territories, sheets, callgates, targets, result counts, and calls per minute
- denied capabilities are omitted from `tools/list`

This is intentional. The public surface should stay conservative unless you explicitly widen it.

## Current Tool Surface

The table below is generated from the operation attributes in `src/DalamudMCP.Plugin/Operations`.
Run `./build/update-tools.ps1` after adding or renaming a tool; CI checks the generated block with `./build/update-tools.ps1 -Check`.

<!-- generated-tools:start -->
| MCP tool | Operation ID | Source |
| --- | --- | --- |
| `capture_game_screenshot` | `game.screenshot` | [GameScreenshotOperation.cs](./src/DalamudMCP.Plugin/Operations/GameScreenshotOperation.cs) |
| `events_configure` | `events.configure` | [GameEventOperations.cs](./src/DalamudMCP.Plugin/Operations/GameEventOperations.cs) |
| `events_query` | `events.query` | [GameEventOperations.cs](./src/DalamudMCP.Plugin/Operations/GameEventOperations.cs) |
| `events_wait` | `events.wait` | [GameEventOperations.cs](./src/DalamudMCP.Plugin/Operations/GameEventOperations.cs) |
| `game_action_execute` | `game.action.execute` | [GameActionExecuteOperation.cs](./src/DalamudMCP.Plugin/Operations/GameActionExecuteOperation.cs) |
| `game_action_resolve` | `game.action.resolve` | [GameActionResolveOperation.cs](./src/DalamudMCP.Plugin/Operations/GameActionResolveOperation.cs) |
| `game_data_describe_sheet` | `game-data.describe-sheet` | [GameDataOperations.cs](./src/DalamudMCP.Plugin/Operations/GameDataOperations.cs) |
| `game_data_get_row` | `game-data.get-row` | [GameDataOperations.cs](./src/DalamudMCP.Plugin/Operations/GameDataOperations.cs) |
| `game_data_list_sheets` | `game-data.list-sheets` | [GameDataOperations.cs](./src/DalamudMCP.Plugin/Operations/GameDataOperations.cs) |
| `game_data_search` | `game-data.search` | [GameDataOperations.cs](./src/DalamudMCP.Plugin/Operations/GameDataOperations.cs) |
| `get_addon_list` | `addon.list` | [AddonListOperation.cs](./src/DalamudMCP.Plugin/Operations/AddonListOperation.cs) |
| `get_addon_strings` | `addon.strings` | [AddonStringsOperation.cs](./src/DalamudMCP.Plugin/Operations/AddonStringsOperation.cs) |
| `get_addon_tree` | `addon.tree` | [AddonTreeOperation.cs](./src/DalamudMCP.Plugin/Operations/AddonTreeOperation.cs) |
| `get_available_quests` | `quest.available` | [AvailableQuestsOperation.cs](./src/DalamudMCP.Plugin/Operations/AvailableQuestsOperation.cs) |
| `get_chat_log` | `chat.read` | [ChatLogReadOperation.cs](./src/DalamudMCP.Plugin/Operations/ChatLogReadOperation.cs) |
| `get_condition_context` | `condition.context` | [ObservationOperations.cs](./src/DalamudMCP.Plugin/Operations/ObservationOperations.cs) |
| `get_current_quest_objective` | `quest.current-objective` | [CurrentQuestObjectiveOperation.cs](./src/DalamudMCP.Plugin/Operations/CurrentQuestObjectiveOperation.cs) |
| `get_duty_context` | `duty.context` | [DutyContextOperation.cs](./src/DalamudMCP.Plugin/Operations/DutyContextOperation.cs) |
| `get_fate_context` | `fate.context` | [FateContextOperation.cs](./src/DalamudMCP.Plugin/Operations/FateContextOperation.cs) |
| `get_inventory_currencies` | `inventory.currencies` | [ObservationOperations.cs](./src/DalamudMCP.Plugin/Operations/ObservationOperations.cs) |
| `get_inventory_equipment` | `inventory.equipment` | [ObservationOperations.cs](./src/DalamudMCP.Plugin/Operations/ObservationOperations.cs) |
| `get_inventory_items` | `inventory.items` | [ObservationOperations.cs](./src/DalamudMCP.Plugin/Operations/ObservationOperations.cs) |
| `get_inventory_summary` | `inventory.summary` | [InventorySummaryOperation.cs](./src/DalamudMCP.Plugin/Operations/InventorySummaryOperation.cs) |
| `get_nearby_interactables` | `nearby.interactables` | [NearbyInteractablesOperation.cs](./src/DalamudMCP.Plugin/Operations/NearbyInteractablesOperation.cs) |
| `get_party_context` | `party.context` | [ObservationOperations.cs](./src/DalamudMCP.Plugin/Operations/ObservationOperations.cs) |
| `get_player_context` | `player.context` | [PlayerContextOperation.cs](./src/DalamudMCP.Plugin/Operations/PlayerContextOperation.cs) |
| `get_quest_status` | `quest.status` | [QuestStatusOperation.cs](./src/DalamudMCP.Plugin/Operations/QuestStatusOperation.cs) |
| `get_session_status` | `session.status` | [SessionStatusOperation.cs](./src/DalamudMCP.Plugin/Operations/SessionStatusOperation.cs) |
| `get_target_context` | `target.context` | [ObservationOperations.cs](./src/DalamudMCP.Plugin/Operations/ObservationOperations.cs) |
| `interact_with_target` | `interact.with.target` | [InteractWithTargetOperation.cs](./src/DalamudMCP.Plugin/Operations/InteractWithTargetOperation.cs) |
| `invoke_plugin_ipc` | `plugin.ipc` | [SafeInvokePluginIpcOperation.cs](./src/DalamudMCP.Plugin/Operations/SafeInvokePluginIpcOperation.cs) |
| `move_to_entity` | `move.to.entity` | [MoveToEntityOperation.cs](./src/DalamudMCP.Plugin/Operations/MoveToEntityOperation.cs) |
| `move_to_nearby_interactable` | `move.to.nearby.interactable` | [MoveToNearbyInteractableOperation.cs](./src/DalamudMCP.Plugin/Operations/MoveToNearbyInteractableOperation.cs) |
| `plugin_data_poll` | `plugin.data.poll` | [PluginDataPollOperation.cs](./src/DalamudMCP.Plugin/Operations/PluginDataPollOperation.cs) |
| `plugin_data_subscribe` | `plugin.data.subscribe` | [PluginDataSubscribeOperation.cs](./src/DalamudMCP.Plugin/Operations/PluginDataSubscribeOperation.cs) |
| `plugin_data_unsubscribe` | `plugin.data.unsubscribe` | [PluginDataUnsubscribeOperation.cs](./src/DalamudMCP.Plugin/Operations/PluginDataUnsubscribeOperation.cs) |
| `plugin_describe` | `plugin.inspect.describe` | [PluginManagementOperations.cs](./src/DalamudMCP.Plugin/Operations/PluginManagementOperations.cs) |
| `plugin_lifecycle` | `plugin.lifecycle.control` | [PluginManagementOperations.cs](./src/DalamudMCP.Plugin/Operations/PluginManagementOperations.cs) |
| `plugin_list` | `plugin.inspect.list` | [PluginManagementOperations.cs](./src/DalamudMCP.Plugin/Operations/PluginManagementOperations.cs) |
| `plugin_package` | `plugin.package.control` | [PluginManagementOperations.cs](./src/DalamudMCP.Plugin/Operations/PluginManagementOperations.cs) |
| `reload_plugin` | `plugin.reload` | [PluginReloadOperation.cs](./src/DalamudMCP.Plugin/Operations/PluginReloadOperation.cs) |
| `select_addon_menu_item` | `addon.select.menu-item` | [AddonSelectMenuItemOperation.cs](./src/DalamudMCP.Plugin/Operations/AddonSelectMenuItemOperation.cs) |
| `send_addon_callback_values` | `addon.callback.values` | [AddonCallbackValuesOperation.cs](./src/DalamudMCP.Plugin/Operations/AddonCallbackValuesOperation.cs) |
| `send_addon_event` | `addon.event` | [AddonEventOperation.cs](./src/DalamudMCP.Plugin/Operations/AddonEventOperation.cs) |
| `send_addon_input` | `addon.input` | [AddonInputOperation.cs](./src/DalamudMCP.Plugin/Operations/AddonInputOperation.cs) |
| `slash_command` | `command.slash` | [SlashCommandOperation.cs](./src/DalamudMCP.Plugin/Operations/SlashCommandOperation.cs) |
| `target_object` | `target.object` | [TargetObjectOperation.cs](./src/DalamudMCP.Plugin/Operations/TargetObjectOperation.cs) |
| `teleport_to_aetheryte` | `teleport.to.aetheryte` | [TeleportToAetheryteOperation.cs](./src/DalamudMCP.Plugin/Operations/TeleportToAetheryteOperation.cs) |
| `unsafe_invoke_plugin_ipc` | `unsafe.invoke.plugin-ipc` | [UnsafeInvokePluginIpcOperation.cs](./src/DalamudMCP.Plugin/Operations/UnsafeInvokePluginIpcOperation.cs) |
| `use_duty_action` | `duty.action` | [DutyActionOperation.cs](./src/DalamudMCP.Plugin/Operations/DutyActionOperation.cs) |
<!-- generated-tools:end -->

The generic Excel search is bounded to 100 returned rows, 10,000 scanned rows, and 500 ms per call. Plugin package operations only select manifests already present in Dalamud's configured repositories; the MCP request cannot supply an arbitrary download URL or CLR method name.

Manifold remains at 1.0.0 because it owns the operation and CLI layer, not MCP version negotiation. The MCP-facing layer uses the official C# SDK 2.1.0 and adds transport-independent effect, permission, schema, and execution metadata to the internal protocol descriptor.

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

- [`src/DalamudMCP.Plugin/bin/Debug/DalamudMCP.dll`](./src/DalamudMCP.Plugin/bin/Debug/DalamudMCP.dll)
- [`src/DalamudMCP.Plugin/bin/Debug/DalamudMCP.json`](./src/DalamudMCP.Plugin/bin/Debug/DalamudMCP.json)

Load that plugin in Dalamud, then open its configuration window.

### 3. Try The CLI

```powershell
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- player context
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- session status --json
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- inventory summary
```

Normal CLI use auto-discovers the active plugin instance from `active-instance.json`.

`--pipe <name>` is still supported as an advanced override for debugging or multi-instance scenarios.

Self-update and self-uninstall require the external supervisor so the verification process survives the plugin stopping or restarting:

```powershell
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- supervise self-package --action update --timeout-seconds 90
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- supervise self-package --action uninstall --timeout-seconds 90
```

The `plugin.self.manage` capability must be explicitly confirmed or allowed before either command can proceed.

### 4. Run MCP

`stdio` MCP:

```powershell
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- serve mcp
```

Local HTTP MCP:

```powershell
$env:DALAMUD_MCP_HTTP_TOKEN = '<token>'
dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- serve http --port 38473
```

Default endpoint:

- `http://127.0.0.1:38473/mcp`

The plugin UI can also start and stop the bundled HTTP MCP server for you. It generates a 256-bit bearer token, passes it to the child process without putting it on the command line, and provides separate buttons for copying the endpoint and token. HTTP clients must send `Authorization: Bearer <token>`.

The HTTP server binds only to `127.0.0.1`, rejects non-loopback `Origin` values, and delegates MCP version negotiation to the official C# SDK transport. The named-pipe bridge is current-user-only and rejects frames larger than 16 MiB.

See [`design/capability-kernel.md`](./design/capability-kernel.md) for capability policy configuration and the current limits of the implementation.

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

- [`src/DalamudMCP.Protocol`](./src/DalamudMCP.Protocol)
- [`src/DalamudMCP.Cli`](./src/DalamudMCP.Cli)
- [`src/DalamudMCP.Plugin`](./src/DalamudMCP.Plugin)

Test projects:

- [`tests/DalamudMCP.Protocol.Tests`](./tests/DalamudMCP.Protocol.Tests)
- [`tests/DalamudMCP.Cli.Tests`](./tests/DalamudMCP.Cli.Tests)
- [`tests/DalamudMCP.Plugin.Tests`](./tests/DalamudMCP.Plugin.Tests)
- [`tests/DalamudMCP.Plugin.Operations.Tests`](./tests/DalamudMCP.Plugin.Operations.Tests)
