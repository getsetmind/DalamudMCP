# World Action Spec

## 1. 目的

この文書は、プレイヤー移動と world interaction の汎用 action capability を定義する。

## 2. スコープ

### 含む

- world point への移動
- entity への接近
- 現在 target との interaction
- 最寄り interactable との interaction
- 移動停止

### 含まない

- パス探索全自動
- zone を跨ぐ導線全自動
- 戦闘時の移動最適化
- 条件分岐付き複合行動

## 3. capability 一覧

### `move_to_world_point`

入力:

- territoryId
- x
- y
- z
- tolerance

出力:

- `ActionResult`

### `move_to_entity`

入力:

- entity selector
- stop distance

### `stop_movement`

入力:

- なし、または optional operationId

### `interact_with_target`

入力:

- optional target validation

### `interact_with_nearest`

入力:

- interactable type
- max distance

## 4. precondition

### movement

- player controllable
- not loading
- no cutscene lock
- coordinates valid
- territory match

### interaction

- target exists
- target interactable
- target in range
- no modal blocking UI

## 5. internal ports

- `IPlayerMovementController`
- `IInteractionController`
- `ITargetResolver`
- `IWorldStateReader`

## 6. 失敗分類

- `player_not_ready`
- `wrong_territory`
- `target_not_found`
- `target_not_interactable`
- `blocked_by_ui`
- `conflicting_action`
- `timed_out`

## 7. audit summary 例

- `move_to_world_point territory=144 x=12.3 y=0.0 z=-4.1`
- `interact_with_target target="Mahjong Attendant"`

## 8. 結論

world action は汎用化できるが、上位 workflow は `DalamudMCP` に入れない。
