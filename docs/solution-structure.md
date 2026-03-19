# Solution Structure

## 1. 目的

この文書は、`DalamudMCP` の solution / project 構成を固定し、実装開始後に責務が崩れるのを防ぐためのもの。

## 2. ルート構成

```text
DalamudMCP/
├─ src/
├─ tests/
├─ build/
├─ docs/
├─ .github/
│  └─ workflows/
├─ Directory.Build.props
├─ Directory.Build.targets
├─ .editorconfig
├─ global.json
└─ DalamudMCP.sln
```

## 3. production projects

### `DalamudMCP.Domain`

責務:

- entity
- value object
- domain policy
- sensitivity model
- exposure model

参照:

- なし

参照される先:

- すべて

### `DalamudMCP.Application`

責務:

- use case
- query / command
- policy evaluation
- validation
- abstraction port

参照:

- `DalamudMCP.Domain`

### `DalamudMCP.Contracts`

責務:

- plugin と host 間の内部 bridge contract
- transport 非依存の DTO

参照:

- `DalamudMCP.Domain`
  または完全独立

原則:

- MCP DTO を置かない
- Dalamud 型を置かない

### `DalamudMCP.Infrastructure`

責務:

- Dalamud adapter
- native UI adapter
- settings persistence
- audit logging
- named pipe bridge

参照:

- `DalamudMCP.Application`
- `DalamudMCP.Domain`
- `DalamudMCP.Contracts`

### `DalamudMCP.Plugin`

責務:

- `IDalamudPlugin` entrypoint
- composition root
- plugin windows
- Dalamud event hookup

参照:

- `DalamudMCP.Application`
- `DalamudMCP.Infrastructure`
- `DalamudMCP.Contracts`

### `DalamudMCP.Host`

責務:

- MCP server metadata
- tool / resource exposure
- initialize handshake
- stdio transport
- 将来の HTTP transport

参照:

- `DalamudMCP.Application`
- `DalamudMCP.Infrastructure`
- `DalamudMCP.Contracts`

## 4. test projects

### `DalamudMCP.Domain.Tests`

- domain rule
- invariants
- normalization

### `DalamudMCP.Application.Tests`

- use case
- allowlist / deny list
- policy
- unavailable handling

### `DalamudMCP.Contracts.Tests`

- contract serialization
- version compatibility

### `DalamudMCP.Infrastructure.Tests`

- settings store
- bridge serializer
- audit persistence

### `DalamudMCP.Host.Tests`

- MCP initialize
- tool list
- resource list
- schema validation

### `DalamudMCP.Plugin.Tests`

- composition root smoke test
- settings UI state mapping

### `DalamudMCP.ArchitectureTests`

- dependency rule
- forbidden namespace rule
- layering rule

## 5. build assets

### `build/`

内容候補:

- local scripts
- CI helper scripts
- coverage merge scripts
- report generation scripts

ルール:

- build tooling はここへ寄せる
- project 直下に散らさない

## 6. root configuration files

### `global.json`

目的:

- SDK pinning
- CI とローカルの再現性確保

方針:

- exact SDK version を pin
- `rollForward = latestFeature`

これは 2026-03-19 時点の Microsoft Learn の `global.json` 推奨例に沿う。

### `Directory.Build.props`

目的:

- 共通 target framework
- nullable
- analyzers
- warnings as errors
- test / coverage 共通設定

### `Directory.Build.targets`

目的:

- CI 向け補助 target
- coverage fail-fast
- report 出力補助

### `.editorconfig`

目的:

- formatter / style / analyzer の単一真実源

## 7. naming rules

- project 名と root namespace は一致
- `*.Tests` は test assembly
- `*.ArchitectureTests` は依存検証専用
- `Contracts` は internal bridge 契約専用

## 8. 導入順序

実装時の順序は固定する。

1. `Domain`
2. `Application`
3. `Contracts`
4. `Infrastructure`
5. `Host`
6. `Plugin`
7. tests / CI の強化

理由:

- delivery layer から書き始めると依存が汚染されやすい

## 9. 結論

この solution は project 数が多いが、`Dalamud` 依存と `MCP` 依存を隔離し、allowlist / policy / UI introspection を安全に保つには必要な分割。
