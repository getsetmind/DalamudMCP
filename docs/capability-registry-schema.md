# Capability Registry Schema

## 1. 目的

この文書は、tool / resource / addon exposure の single source of truth となる registry の設計を定義する。

## 2. 役割

registry は次を一元管理する。

- capability identity
- tool / resource 対応
- profile
- sensitivity
- default enablement
- deny 状態
- 設定 UI 表示情報

## 3. 設計方針

- code-first でも data-first でもよいが、論理モデルは 1 つにする
- `Host` と `Plugin UI` は同じ registry から情報を得る
- hard deny も registry に乗る
- 実行時 policy は registry ではなく `ExposurePolicy` に置く

## 4. 論理モデル

```text
CapabilityRegistry
├─ Capabilities[]
├─ ResourceBindings[]
├─ ToolBindings[]
└─ AddonMetadata[]
```

## 5. CapabilityDefinition

```text
CapabilityDefinition
├─ Id: string
├─ DisplayName: string
├─ Description: string
├─ Category: enum
├─ Sensitivity: enum
├─ Profile: enum
├─ DefaultEnabled: bool
├─ RequiresConsent: bool
├─ Denied: bool
├─ SupportsTool: bool
├─ SupportsResource: bool
├─ Tags: string[]
└─ Version: string
```

### ルール

- `Denied = true` の capability は settings で覆せない
- `SupportsTool = false` なら tool binding を持たない
- `SupportsResource = false` なら resource binding を持たない

## 6. ToolBinding

```text
ToolBinding
├─ CapabilityId: string
├─ ToolName: string
├─ InputSchemaId: string
├─ OutputSchemaId: string
├─ HandlerType: string
└─ Experimental: bool
```

### ルール

- `ToolName` は MCP surface 上で一意
- `CapabilityId` との 1:1 を原則とする
- 1 capability に複数 tool をぶら下げるのは例外扱い

## 7. ResourceBinding

```text
ResourceBinding
├─ CapabilityId: string
├─ UriTemplate: string
├─ MimeType: string
├─ ProviderType: string
└─ SupportsSubscription: bool
```

### ルール

- `CapabilityId` と resource は 1:0..n
- v1 では subscription は既定 false

## 8. AddonMetadata

```text
AddonMetadata
├─ AddonName: string
├─ DisplayName: string
├─ Category: enum
├─ Sensitivity: enum
├─ DefaultEnabled: bool
├─ Denied: bool
├─ Notes: string
├─ Tags: string[]
└─ SupportedIntrospectionModes: string[]
```

### SupportedIntrospectionModes 例

- `summary`
- `tree`
- `strings`
- `numbers`

## 9. enum 候補

### CapabilityCategory

- `Player`
- `Duty`
- `Inventory`
- `Ui`
- `Party`
- `Target`
- `Crafting`
- `Gathering`
- `Market`
- `System`
- `Experimental`

### Sensitivity

- `low`
- `medium`
- `high`
- `blocked`

### Profile

- `baseline`
- `experimental-private`

## 10. registry の責務外

registry は次を持たない。

- 現在の ON/OFF 状態
- 現在の接続状態
- 実行中セッション情報
- audit log 実体

それらは:

- `ExposurePolicy`
- `SessionState`
- `AuditEvent`

へ分ける。

## 11. 検証ルール

registry に対して自動テストで検証すべきこと:

- `Id` が一意
- `ToolName` が一意
- denied capability に enabled default がない
- blocked addon に enabled default がない
- missing schema がない
- missing handler がない
- profile と sensitivity の組み合わせが妥当

## 12. 運用方針

### v1 推奨

- registry は code-defined
- ただし論理的には schema を持つ

理由:

- compile-time safety
- refactor しやすい
- handler type 整合を取りやすい

### 将来

- addon metadata だけ data-driven 化
- capability registry は code-defined のままでもよい

## 13. 例

```text
CapabilityDefinition
  Id = "ui.addonTree"
  DisplayName = "Addon Tree"
  Category = Ui
  Sensitivity = high
  Profile = baseline
  DefaultEnabled = false
  RequiresConsent = true
  Denied = false
  SupportsTool = true
  SupportsResource = true
```

```text
ToolBinding
  CapabilityId = "ui.addonTree"
  ToolName = "get_addon_tree"
  InputSchemaId = "schemas.tools.get_addon_tree.input"
  OutputSchemaId = "schemas.tools.get_addon_tree.output"
  HandlerType = "GetAddonTreeToolHandler"
  Experimental = false
```

## 14. 結論

`CapabilityRegistry` は、将来の拡張性と安全性の要。ここが single source of truth になっていないと、tool 追加時に必ず settings / host / tests がずれる。
