# Player Context Spec

## 1. 目的

`get_player_context` は、外部 MCP client が FFXIV の「今の自キャラ文脈」を安全に把握するための最小ツールです。

初期版では、自キャラの read-only 状態のみを返し、戦闘支援や自動操作に転用されやすい高頻度情報は出しません。

## 2. 基本方針

- 対象は常に自キャラのみ
- 他プレイヤーの恒久識別子は返さない
- tool と resource の両方で表現可能にする
- 値が取れない場合は `available = false` と理由を返す
- raw game state ではなく正規化済み snapshot を返す

## 3. 提供形態

### Tool

- name: `get_player_context`
- purpose: 明示的に現在の自キャラ文脈を取得する

### Resource

- uri: `ffxiv://player/context`
- purpose: 現在 snapshot の参照

同一の内部 snapshot を、tool と resource の 2 面で公開する。

## 4. v1 の入力

初期版の input は空 object を推奨する。

理由:

- ability surface を狭くする
- schema 互換性を保ちやすい
- client ごとの差を減らす

想定 schema:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false
}
```

## 5. v1 の出力項目

### Envelope

- `available`: bool
- `reason`: string | null
- `capturedAt`: RFC 3339 timestamp
- `snapshotAgeMs`: integer
- `contractVersion`: string

### Identity

- `characterName`: string
- `homeWorld`: string | null
- `currentWorld`: string | null

### Job

- `classJobId`: integer | null
- `classJobName`: string | null
- `level`: integer | null

### Location

- `territoryId`: integer | null
- `territoryName`: string | null
- `mapId`: integer | null
- `mapName`: string | null
- `position`: object | null

`position` は詳細座標ではなく概略とする。

- `x`: number | null
- `y`: number | null
- `z`: number | null
- `precision`: string

`precision` の初期値は `coarse` を想定する。

### Activity

- `inCombat`: bool | null
- `inDuty`: bool | null
- `isCrafting`: bool | null
- `isGathering`: bool | null
- `isMounted`: bool | null
- `isMoving`: bool | null

### Context Summary

- `zoneType`: string | null
- `contentStatus`: string | null
- `summaryText`: string

## 6. あえて含めない項目

- HP / MP のリアルタイム値
- GCD やアビリティ cooldown
- precise heading / exact movement vectors
- action queue
- 他者ターゲット詳細
- permanent character ID

これらは将来の自動化や戦闘支援に寄りやすいので v1 から外す。

## 7. `summaryText` の役割

`summaryText` は、schema 化されたフィールドを補助する自然文サマリです。

例:

`Lv100 White Mage in Limsa Lominsa Lower Decks, not in combat, not in duty.`

これは LLM 側の即時理解を助けるための補助であり、正本は structured fields とする。

## 8. Freshness ルール

- snapshot は pull 時に毎回 raw 取得しない
- plugin 側で安全な周期で更新した直近値を返す
- host は snapshot の age をそのまま返す

初期の設計目安:

- 通常更新: 500ms - 1000ms
- zone change や job change 時はイベント起点で即時更新

数値は現段階の設計案であり、実装時に再評価する。

## 9. 失敗時の返し方

### 例

- `available = false, reason = "player_not_ready"`
- `available = false, reason = "not_logged_in"`
- `available = false, reason = "capability_disabled"`

MCP tool error にするより、まずは正常レスポンス内で unavailable を返す方が扱いやすい。

ただし契約違反や不正入力は通常の protocol error にする。

## 10. Privacy ルール

- 返す主体は自キャラに限定
- 他者情報は summary にも混ぜない
- exact 位置ではなく coarse 位置
- 監査ログには tool 名と timestamp を残し、本文の全文保存は後で判断

## 11. 将来拡張

v2 候補:

- `instanceId`
- `grandCompany`
- `jobRole`
- `sanctuaryState`
- `housingZoneState`

ただし、v1 を出す前に項目を増やしすぎない。

## 12. 初期 output schema 叩き台

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["available", "capturedAt", "contractVersion", "summaryText"],
  "properties": {
    "available": { "type": "boolean" },
    "reason": { "type": ["string", "null"] },
    "capturedAt": { "type": "string", "format": "date-time" },
    "snapshotAgeMs": { "type": "integer", "minimum": 0 },
    "contractVersion": { "type": "string" },
    "characterName": { "type": ["string", "null"] },
    "homeWorld": { "type": ["string", "null"] },
    "currentWorld": { "type": ["string", "null"] },
    "classJobId": { "type": ["integer", "null"] },
    "classJobName": { "type": ["string", "null"] },
    "level": { "type": ["integer", "null"], "minimum": 1, "maximum": 100 },
    "territoryId": { "type": ["integer", "null"] },
    "territoryName": { "type": ["string", "null"] },
    "mapId": { "type": ["integer", "null"] },
    "mapName": { "type": ["string", "null"] },
    "position": {
      "type": ["object", "null"],
      "additionalProperties": false,
      "required": ["x", "y", "z", "precision"],
      "properties": {
        "x": { "type": ["number", "null"] },
        "y": { "type": ["number", "null"] },
        "z": { "type": ["number", "null"] },
        "precision": { "type": "string", "enum": ["coarse"] }
      }
    },
    "inCombat": { "type": ["boolean", "null"] },
    "inDuty": { "type": ["boolean", "null"] },
    "isCrafting": { "type": ["boolean", "null"] },
    "isGathering": { "type": ["boolean", "null"] },
    "isMounted": { "type": ["boolean", "null"] },
    "isMoving": { "type": ["boolean", "null"] },
    "zoneType": { "type": ["string", "null"] },
    "contentStatus": { "type": ["string", "null"] },
    "summaryText": { "type": "string" }
  }
}
```
