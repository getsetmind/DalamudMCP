# Data Contract Outline

この文書は、プラグインと外部ホストの間でやり取りする内部契約の粗い輪郭を定義します。型はまだ固定しません。

## 原則

- internal contract と MCP response schema は分ける
- ゲーム内の生値をそのまま外に出さない
- contract は version を持つ
- optional 項目を許容し、UI や game state の揺れに耐える

## 契約層

### 1. Snapshot Contract

プラグイン内部で確定済みの文脈。

例:

- `PlayerContextSnapshot`
- `LocationContextSnapshot`
- `DutyContextSnapshot`
- `InventorySummarySnapshot`

特徴:

- 取得済みデータ
- 画面やフレーム状態に依存する欠損を許容
- timestamp を持つ

### 2. Query Contract

外部ホストがプラグインへ送る問い合わせ。

例:

- `GetPlayerContext`
- `GetDutyContext`
- `GetInventorySummary`

特徴:

- シンプルな request/response
- サーバー側キャッシュがあっても契約は変えない

### 3. Policy Contract

どの capability が許可されているかを外部ホストが取得するための契約。

例:

- `GetCapabilityState`
- `GetSessionStatus`

特徴:

- 外部ホストが勝手に tool を expose しないための安全弁

## 返却方針

### 許容する返却

- `available = false`
- `reason = "not_in_duty"`
- `reason = "capability_disabled"`
- `reason = "data_not_ready"`

### 避ける返却

- 生の game memory dump
- add-on 内部構造の露出
- 取得失敗の詳細すぎる内部例外

## versioning 方針

- `contractVersion` を持つ
- breaking change は major を上げる
- host 側は古い contract を拒否できる

## 将来の MCP 変換例

内部:

- `InventorySummarySnapshot`

外部:

- MCP tool `get_inventory_summary`
- MCP resource `inventory://summary`

同じ内部データを、tool と resource に別表現で出してよい。
