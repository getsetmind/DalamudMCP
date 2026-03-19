# UI Introspection Spec

## 1. 目的

この文書は、FFXIV ネイティブ UI 上の `AtkUnitBase`、`AtkResNode`、`StringArrayData`、`NumberArrayData` などを、MCP から安全に観測するための設計を定義する。

ここで扱うのは read-only 観測のみ。UI の書き換えやクリック代行は別扱いにする。

## 2. 公式に依拠する前提

2026-03-19 時点の Dalamud 公式ドキュメントでは:

- `IAddonLifecycle` は addon の各状態変化を監視できる
- `IAddonEventManager` は native UI node に対する custom event 登録を提供する
- `AddonEvent` には `PreSetup`, `PostSetup`, `PreRequestedUpdate`, `PostRequestedUpdate` などがある
- `AddonRequestedUpdateArgs` は `StringArrayData` / `NumberArrayData` の更新文脈に関わる
- `AtkUnitBasePtr` と `AtkValuePtr` などの NativeWrapper がある

設計への影響:

- addon の観測点は `AddonLifecycle` を軸にする
- 汎用的な node tree 取得は `AtkUnitBasePtr` / native node traversal で扱う
- setup 時の `AtkValue` と requested update 時の array 変化は分けて考える

## 3. スコープ

### 含む

- addon 一覧
- addon の可視状態
- addon の node tree 概要
- node id / node type / 可視状態 / bounds 概略
- text node の表示文字列
- setup 時の `AtkValueSpan` 観測
- requested update 時の `StringArrayData` / `NumberArrayData` の観測

### 含まない

- 任意 node への書込み
- click 代行
- hidden UI の強制表示
- dialog 自動送り
- UI 状態を使った gameplay automation

## 4. 想定リソース

### `ffxiv://ui/addons`

開いている addon 一覧の要約。

### `ffxiv://ui/addon/{name}`

特定 addon の基本情報。

### `ffxiv://ui/addon/{name}/tree`

node tree の簡略表現。

### `ffxiv://ui/addon/{name}/strings`

現在保持されている string table の観測結果。

### `ffxiv://ui/addon/{name}/numbers`

現在保持されている number table の観測結果。

## 5. 想定ツール

### `get_addon_list`

- visible / ready な addon 一覧

### `get_addon_tree`

- 対象 addon 名を受け取り、node tree を返す

### `get_addon_strings`

- 対象 addon 名を受け取り、string table を返す

### `get_addon_values`

- setup 時点の `AtkValue` 由来の値を返す

## 6. データモデル

### AddonSummary

- `name`
- `isReady`
- `isVisible`
- `address`
- `capturedAt`

`address` は raw pointer を外部へそのまま返さない。必要なら masked するか非公開にする。

### NodeSummary

- `nodeId`
- `nodeType`
- `visible`
- `x`
- `y`
- `width`
- `height`
- `text`
- `childCount`

### StringTableSnapshot

- `addonName`
- `capturedAt`
- `entries`

`entries` は index と decoded text の対。

## 7. string table の扱い

string table は UI 実装依存が強い。したがって:

- addon ごとに index 意味が違う
- index の意味は generic contract に埋め込まない
- raw index と decoded text のペアだけ返す
- `SeString` / macro 文字列は可能なら評価済み表示文字列と raw を併記する

2025-03-25 公開の Dalamud v12 情報では experimental な `ISeStringEvaluator` が追加されているため、将来はこれを利用して UI 内 macro を評価する余地がある。

これは公式情報に基づく将来設計案であり、実装可否は後で確認が必要。

## 8. node tree の粒度

完全ダンプは避ける。v1 の標準は shallow tree。

### shallow tree

- ルート
- 子
- 孫

### deep tree

- 明示 opt-in のみ
- 大型 addon では切り詰める

理由:

- ペイロード肥大化を避ける
- UI 実装依存のノイズを抑える
- LLM が読める密度にする

## 9. 監視モデル

### pull-only 基本

- MCP call 時に最新 snapshot を返す

### plugin 内更新トリガ

- `PostSetup`
- `PostRequestedUpdate`
- `PostUpdate`
- `PostDraw`

`PostDraw` は高頻度になるので、常用ではなく必要最小限に抑える。

## 10. プライバシーと安全性

- chat log 全文のような高感度 UI は明示 deny list に入れる
- tell, friend, party finder private fields のような対象は既定で除外
- account ID 等の恒久識別子は取得対象から外す
- addon 名 allowlist を持つ

## 11. allowlist の扱い

allowlist はコード固定ではなく、plugin 設定 UI から選択可能にする。

### 初期状態

- 初期状態では安全寄りの推奨セットを preselect する
- コミュニケーション系 UI や対人情報の濃い UI は default OFF

### 初期推奨セット案

- `_ToDoList`
- `Inventory`
- `Character`
- `ContentsInfo`
- `Journal`
- `RecipeNote`

### UI 要件

- addon 名の一覧を表示する
- 検索できる
- `Recommended`
- `Advanced`
- `High Sensitivity`
  の区分を出す
- addon ごとに ON/OFF できる
- 変更時に「この UI は個人情報や会話を含む可能性がある」と警告できる

### 制御ルール

- deny list 対象は設定 UI からも有効化できない
- `High Sensitivity` は baseline profile では選択不可にしてよい
- 変更は即時反映ではなく Apply でもよい

初期版では、コミュニケーション系 UI や対人情報の濃い UI は allowlist に入れない。

## 12. 結論

`Atk node` と `string table` の観測は設計可能。ただし、generic に何でも抜けるようにせず、

- addon allowlist
- shallow tree
- decoded read-only snapshot
- high-sensitivity UI deny list

で縛るべき。

## Sources

- https://dalamud.dev/plugin-development/how-tos/AddonLifecycle/
- https://dalamud.dev/plugin-development/how-tos/AddonEventManager/
- https://dalamud.dev/api/Dalamud.Game.Addon.Lifecycle/Enums/AddonEvent
- https://dalamud.dev/api/api14/Dalamud.Game.Addon.Lifecycle.AddonArgTypes/Classes/AddonArgs
- https://dalamud.dev/api/Dalamud.Game.NativeWrapper/Structs/AtkUnitBasePtr/
- https://dalamud.dev/api/Dalamud.Game.NativeWrapper/Structs/AtkValuePtr/
- https://dalamud.dev/api/Dalamud.Game.Text.Evaluator/Structs/SeStringParameter
- https://dalamud.dev/versions/v12/
