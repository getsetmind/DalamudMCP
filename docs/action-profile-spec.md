# Action Profile Spec

## 1. 目的

この文書は、`DalamudMCP` における操作系 capability の共通仕様を定義する。

## 2. プロファイル

- 名称: `ActionProfile`
- 性質: `experimental-private`
- default: OFF
- transport: MCP tools のみ

## 3. 共通制約

- capability ごとの明示有効化が必要
- 監査ログ必須
- rate limit 必須
- precondition check 必須
- duplicate suppression 必須
- baseline client とは別公開面で扱う

## 4. 共通レスポンス envelope

```text
ActionResult
├─ operationId
├─ accepted
├─ completed
├─ status
├─ reason
├─ startedAt
├─ completedAt
└─ summaryText
```

### status 候補

- `accepted`
- `completed`
- `rejected`
- `timed_out`
- `not_ready`
- `disabled`
- `denied`
- `failed`

## 5. 共通 precondition

action 実行前に最低でも次を判定する。

- profile enabled
- capability enabled
- plugin state ready
- player available
- no conflicting operation
- target / addon / coordinates が妥当

## 6. 共通 guard

- concurrent action 禁止または制御
- action timeout
- cooldown
- duplicate request suppression
- high sensitivity action の extra consent

## 7. 分類

### Movement Action

- 移動開始
- 移動停止
- 位置移動

### Interaction Action

- target interaction
- nearest interaction

### UI Action

- addon event send
- entry select
- button press

## 8. settings UI 上の扱い

- `Action` タブとして独立表示
- baseline から視覚的に分離
- warning 文を常時表示
- capability ごとに個別 ON/OFF
- hard deny は表示しても押せない

## 9. 監査の粒度

action 系は read-only より高い粒度で記録する。

- request input summary
- target summary
- precondition fail reason
- dispatch result
- completion result

## 10. 結論

`ActionProfile` は observation の延長ではなく、別種のシステムとして扱うべき。
