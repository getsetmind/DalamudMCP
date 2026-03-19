# Clean Architecture

## 1. 方針

`DalamudMCP` は、Dalamud plugin でありながら、内部は徹底して Clean Architecture で分離する。

重要なのは次の 2 点。

1. `IDalamudPlugin` や NativeWrapper 依存を中心に寄せない
2. ビジネスルールと外部技術要素を明確に分離する

Dalamud の公式ドキュメントでは、同一 solution に複数 project を置ける。したがって plugin DLL だけを thin entrypoint にし、内部を複数 project に分ける方針を採る。

これは [Project Layout and Configuration](https://dalamud.dev/plugin-development/project-layout/) の「同じ solution に他 project を置ける」という前提に沿う。

## 2. 依存ルール

依存は必ず外側から内側へ向く。

### 内側

- `Domain`
- `Application`

### 外側

- `Infrastructure`
- `Plugin`
- `Host`

禁止:

- `Domain` が Dalamud 型を参照する
- `Application` が `IDalamudPlugin`、`IGameGui`、`AtkUnitBasePtr` などを参照する
- `Domain` / `Application` が JSON-RPC transport 詳細を知る
- `Host` の MCP DTO を `Domain` へ持ち込む

## 3. レイヤ構成

### `DalamudMCP.Domain`

責務:

- エンティティ
- 値オブジェクト
- ドメインポリシー
- capability 分類
- sensitivity 分類

例:

- `CapabilityId`
- `SensitivityLevel`
- `ExposurePolicy`
- `PlayerContextSnapshot`

依存:

- BCL のみを原則とする

### `DalamudMCP.Application`

責務:

- use case
- query / command handler
- port interface
- validation
- authorization / policy evaluation

例:

- `GetPlayerContextQuery`
- `GetAddonTreeQuery`
- `ResolveExposurePolicyUseCase`
- `CanExposeCapabilityPolicy`

依存:

- `Domain`

### `DalamudMCP.Infrastructure`

責務:

- Dalamud service adapter
- Native UI adapter
- Named Pipe adapter
- file settings persistence
- audit log persistence

例:

- `DalamudClientStateAdapter`
- `AtkTreeReader`
- `NamedPipeBridgeClient`
- `JsonSettingsStore`

依存:

- `Application`
- `Domain`
- Dalamud / MCP / IO 実装詳細

### `DalamudMCP.Plugin`

責務:

- `IDalamudPlugin` entrypoint
- DI composition root
- plugin window wiring
- Dalamud lifecycle hookup

ルール:

- 極薄であること
- use case 実行以外の判断を持たない

### `DalamudMCP.Host`

責務:

- MCP transport
- MCP schema
- protocol initialize
- tool / resource registration
- bridge access

ルール:

- `Application` を使って処理する
- transport ごとの差は host の外側に閉じ込める

## 4. 推奨 solution 構成

```text
DalamudMCP.sln
├─ src/
│  ├─ DalamudMCP.Domain/
│  ├─ DalamudMCP.Application/
│  ├─ DalamudMCP.Infrastructure/
│  ├─ DalamudMCP.Plugin/
│  ├─ DalamudMCP.Host/
│  └─ DalamudMCP.Contracts/
├─ tests/
│  ├─ DalamudMCP.Domain.Tests/
│  ├─ DalamudMCP.Application.Tests/
│  ├─ DalamudMCP.Infrastructure.Tests/
│  ├─ DalamudMCP.Host.Tests/
│  ├─ DalamudMCP.Plugin.Tests/
│  └─ DalamudMCP.ArchitectureTests/
└─ build/
```

`DalamudMCP.Contracts` は internal bridge 契約だけを置く。MCP の外向け DTO は `Host` に閉じ込める。

## 5. DTO の分離

最低でも DTO は 3 種類に分ける。

1. Domain model
2. Internal bridge contract
3. MCP transport DTO

この 3 つを混ぜない。

## 6. 例外

例外もレイヤで分ける。

- `DomainException`
- `ApplicationException`
- adapter specific exception

外側の例外を内側へそのまま流さない。

## 7. 設定

設定もレイヤをまたがせない。

- `Domain`: 設定型を持たない
- `Application`: 抽象化された policy input を受ける
- `Infrastructure`: 永続化とロード
- `Plugin`: 設定 UI と変更通知

## 8. テスト容易性のための原則

- 時刻は抽象化する
- ランダムは抽象化する
- Dalamud service は adapter 越しにしか触らない
- static 呼び出しを避ける
- one-shot function より stateful coordinator を明示する

## 9. 禁止事項

- `Plugin.cs` にロジックを書く
- native pointer を domain model に保存する
- UI 表示用文字列を domain rule に埋め込む
- transport DTO を永続化する
- `Application` から直接ログ framework を呼ぶ

## 10. 結論

`DalamudMCP` では、Plugin と Host は delivery mechanism にすぎない。中心は `Domain` と `Application` に置く。

ここが崩れると、Dalamud 依存と MCP 依存が全体に漏れて、後からテスト不能になる。
