# Implementation Order

## 1. 目的

実装開始後に layer を飛ばして `Plugin` や `Host` から作り始めないよう、順序を固定する。

## 2. フェーズ

### Phase 1: Foundation

- `global.json`
- `.editorconfig`
- `Directory.Build.props`
- `DalamudMCP.sln`
- empty project shells
- test project shells
- CI skeleton

完了条件:

- empty solution が build / format / test / coverage の土台を持つ

### Phase 2: Domain

- capability model
- sensitivity model
- exposure policy
- core snapshots

完了条件:

- domain tests が高カバレッジで通る

### Phase 3: Application

- use cases
- policy evaluator
- settings query model

完了条件:

- application tests が通る
- architecture tests が依存方向を保証する

### Phase 4: Contracts

- bridge request / response
- versioning

完了条件:

- contract tests が通る

### Phase 5: Infrastructure

- settings persistence
- audit log persistence
- named pipe abstraction

完了条件:

- integration tests が通る

### Phase 6: Host

- MCP initialize
- tool/resource listing
- first local stdio surface

完了条件:

- host tests が通る
- inspector で surface を検証できる

### Phase 7: Plugin

- entrypoint
- settings UI
- read-only player context wiring

完了条件:

- plugin minimal smoke test
- first end-to-end local flow

### Phase 8: UI Introspection

- addon allowlist UI
- addon snapshot readers
- string table readers

完了条件:

- deny list / allowlist policy tests
- selected addon introspection flow

## 3. やってはいけない順序

- Plugin から先に書く
- Host DTO を domain に流し込む
- native adapter 完成前に policy を省略する

## 4. 結論

この順序を守ると、delivery layer に引っ張られずにコア設計を固定できる。
