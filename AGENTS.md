# AGENTS.md

## Overview

`DalamudMCP` is a local MCP bridge for FFXIV.

Current active projects:

- `src/DalamudMCP.Framework`
- `src/DalamudMCP.Framework.Cli`
- `src/DalamudMCP.Framework.Generators`
- `src/DalamudMCP.Framework.Mcp`
- `src/DalamudMCP.Protocol`
- `src/DalamudMCP.Cli`
- `src/DalamudMCP.Plugin`

The active solution is `DalamudMCP.slnx`.

## Build Scripts

The repository intentionally keeps a small `build/` surface.

Supported scripts:

- `./build/restore.ps1`
- `./build/build.ps1`
- `./build/test.ps1`
- `./build/format.ps1`
- `./build/quality.ps1`
- `./build/architecture.ps1`

Helper scripts:

- `./build/Get-DotNetCommand.ps1`
- `./build/Use-DalamudHome.ps1`

Removed scripts such as Rider-specific wrappers, coverage wrappers, and host smoke wrappers are intentionally not part of the current maintenance surface.

## Formatting

Repository automation uses `dotnet format` only.

- CI and `./build/format.ps1` rely on `dotnet format`
- there is no `format-rider.ps1`
- there is no `inspect-rider.ps1`

If you use Rider locally, use the IDE features directly instead of repository scripts:

1. `Code > Reformat Code`
2. `Code > Cleanup Code`

That is a local editor workflow only. Do not reintroduce Rider-specific build scripts unless the repository explicitly needs them again.

## Dalamud SDK

`src/DalamudMCP.Plugin` uses `Dalamud.NET.Sdk`.

If Dalamud is installed in a non-default location, provide `DALAMUD_HOME` or pass `-DalamudHome` to the build scripts.

Examples:

```powershell
$env:DALAMUD_HOME = 'C:\path\to\Hooks\dev'
.\build\build.ps1 -NoRestore -DalamudHome $env:DALAMUD_HOME
.\build\test.ps1 -NoBuild -DalamudHome $env:DALAMUD_HOME
```

## Release

Plugin release packaging is manual.

```powershell
.\.dotnet\dotnet.exe build .\src\DalamudMCP.Plugin\DalamudMCP.Plugin.csproj -c Release
```

Upload `src/DalamudMCP.Plugin/bin/Release/DalamudMCP/latest.zip` manually when cutting a release.
