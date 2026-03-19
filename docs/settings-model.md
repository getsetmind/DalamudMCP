# Settings Model

## 1. 目的

`DalamudMCP` の公開境界を、コード固定ではなく plugin 設定 UI から管理できるようにする。

対象は次の 3 つ。

1. MCP tools
2. MCP resources
3. UI introspection addon allowlist

## 2. 基本方針

- デフォルトは最小公開
- 推奨セットを用意する
- ユーザーが個別選択できる
- 高感度項目は追加確認を要求する
- deny list は設定画面からも有効化できない

## 3. 設定階層

### Global

- `pluginEnabled`
- `baselineProfileEnabled`
- `experimentalProfileEnabled`
- `auditLoggingEnabled`
- `panicStopEnabled`

### Tool Exposure

- `enabledTools[]`
- `disabledTools[]`

### Resource Exposure

- `enabledResources[]`
- `disabledResources[]`

### UI Introspection

- `enabledAddons[]`
- `disabledAddons[]`
- `deepTreeEnabled`
- `includeRawStrings`

## 4. UI の見せ方

### セクション

1. Overview
2. Tools
3. Resources
4. UI Addons
5. Privacy & Audit
6. Experimental

### 各一覧の共通要件

- 検索可能
- カテゴリで絞り込み
- 1 件ごとの説明
- 推奨状態の表示
- 高感度バッジの表示
- 個別 ON/OFF

## 5. tool 選択 UI

各 tool に持たせるメタデータ。

- `id`
- `displayName`
- `description`
- `profile`
- `sensitivity`
- `defaultEnabled`
- `requiresConsent`

例:

- `get_player_context`
  - baseline
  - low sensitivity
  - default ON
- `get_addon_strings`
  - baseline
  - medium/high sensitivity
  - default OFF

## 6. resource 選択 UI

tool と同じく、resource も個別公開制御する。

理由:

- 一部 client は resources を自動で読みにいく
- tool は無効でも resource が残っていると境界が崩れる

## 7. addon allowlist UI

addon 一覧には次のメタデータを持たせる。

- `addonName`
- `category`
- `sensitivity`
- `defaultEnabled`
- `notes`

### category 例

- Character
- Inventory
- Crafting
- Duty
- Social
- Chat
- Market

### sensitivity 例

- low
- medium
- high
- blocked

`blocked` は deny list 相当で、UI 上では表示しても有効化不可にする。

## 8. プリセット

### Recommended

- player context 系のみ ON
- inventory summary ON
- duty context ON
- UI introspection は安全な addon のみ ON

### UI Explorer

- Recommended に加え、低〜中感度の UI introspection を広げる

### Locked Down

- player context 以外を極小化

### Experimental

- baseline ではなく別タブで扱う
- 通常プリセットとは混ぜない

## 9. 反映タイミング

推奨:

- 設定変更は UI 上で staged
- `Apply` で反映
- 大きい変更は再接続を要求してよい

理由:

- 意図しない外部公開を防ぐ
- 複数項目変更時の監査がしやすい

## 10. 内部モデル

内部的には、`CapabilityRegistry` と `ExposurePolicy` を分ける。

### CapabilityRegistry

- システムに存在する tool / resource / addon の定義
- 説明、感度、既定値、profile 所属

### ExposurePolicy

- 現在ユーザーが許可した項目
- 設定ファイルと UI が更新する

この分離で、機能定義とユーザー許可状態が混ざらない。

## 11. 監査

設定変更時に残すべきもの。

- timestamp
- changedByUser
- changedItems
- oldValue
- newValue

特に high sensitivity 項目の変更は明示ログ対象にする。

## 12. 結論

allowlist は固定コードではなく、設定 UI ベースにするのが正しい。

ただし、

- deny list は越えられない
- baseline と experimental は分ける
- tools / resources / addons を同じ権限モデルで扱う

この 3 点は崩さない方がよい。
