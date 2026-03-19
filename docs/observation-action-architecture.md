# Observation / Action Architecture

## 1. 目的

`DalamudMCP` を麻雀や特定ゲームロジックに依存させず、汎用の FFXIV observation/action 基盤として拡張する。

ここで重要なのは、read-only の `Observation` と、能動的操作を伴う `Action` を設計上も実装上も明確に分離すること。

## 2. 結論

`DalamudMCP` は次の 2 系統の capability を持つ。

1. `ObservationProfile`
2. `ActionProfile`

`ObservationProfile` は baseline。  
`ActionProfile` は private experimental。

## 3. Observation と Action の違い

### Observation

- ゲーム状態を読む
- UI 状態を読む
- world / actor / addon 文脈を読む
- 副作用を持たない

例:

- `get_player_context`
- `get_addon_tree`
- `get_addon_strings`

### Action

- プレイヤー移動
- インタラクト
- ターゲット変更
- UI イベント送信

特徴:

- 明確な副作用を持つ
- 失敗復旧が必要
- retry / timeout / cooldown が必要
- 誤操作のコストが高い

## 4. 層構造

```text
DalamudMCP
├─ Observation
│  ├─ snapshots
│  ├─ read adapters
│  └─ MCP tools/resources
├─ Action
│  ├─ commands
│  ├─ action planners
│  ├─ precondition checks
│  ├─ execution adapters
│  └─ MCP tools only
└─ Shared Policy
   ├─ capability registry
   ├─ exposure policy
   ├─ audit
   └─ hard deny list
```

## 5. 設計原則

- action capability は baseline に混ぜない
- action capability は resource ではなく tool 中心
- action capability は precondition を必須にする
- action capability は audit を必須にする
- action capability は idempotency と duplicate suppression を考慮する

## 6. ActionProfile に含める汎用 capability

### World Action

- `move_to_world_point`
- `move_to_entity`
- `stop_movement`
- `interact_with_target`
- `interact_with_nearest`

### UI Action

- `send_addon_event`
- `select_addon_entry`
- `press_addon_button`

ただし UI action は node allowlist 前提。

### Optional Action

- `set_target_by_name`
- `set_target_by_entity_id`

これは高リスクなので default OFF。

## 7. ActionProfile に含めないもの

- 自動ループ
- 条件分岐つき workflow
- マクロ実行
- 戦闘回し
- 自動麻雀ロジック
- 画面遷移全自動オーケストレーション

これらは `DalamudMCP` の範囲外。

## 8. 実行モデル

action tool は同期的に「要求を受理した結果」を返す。

返却モデル:

- `accepted`
- `completed`
- `rejected`
- `timed_out`
- `not_available`

長時間 action は `accepted` と `operationId` を返し、別 tool で状態確認してもよい。

## 9. 監査モデル

action 系はすべて audit 必須。

記録項目:

- timestamp
- capabilityId
- input summary
- precondition result
- execution result
- source client

## 10. 破綻防止

Observation と Action を混ぜると、read-only client にも操作面が露出しやすくなる。

そのため:

- registry で profile を分離
- settings UI で分離
- host registry でも分離
- 将来 build も分離

を徹底する。

## 11. 結論

麻雀のような上位 automation は `DalamudMCP` の外側で構築する。  
`DalamudMCP` 自体は、汎用 observation/action primitive だけを提供する。
