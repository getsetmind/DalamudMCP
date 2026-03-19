# FFXIV Use Case Matrix

## 1. 目的

この文書は、FFXIV の主要ユースケースを網羅的に洗い出し、`DalamudMCP` がどこまで対応できるかを評価するためのもの。

分類には Lodestone の Play Guide、UI Guide、Game Manual に見られるカテゴリを参考にしている。

## 2. 分類

### A. 基本プレイ

- キャラクター情報確認
- 現在地確認
- クエスト進行把握
- duty 参加状況確認

### B. 戦闘

- 現在ジョブ確認
- ロール把握
- 戦闘中 / 非戦闘確認
- ターゲット確認

### C. パーティ / マルチプレイ

- パーティ構成確認
- party status 概況
- alliance / cross-world 文脈把握

### D. クラフター / ギャザラー

- 製作中かどうか
- レシピ UI 読取り
- 所持素材の概況
- 採集中かどうか

### E. インベントリ / 装備

- 所持品要約
- 装備状態確認
- リテイナー関連の概況

### F. UI ナビゲーション

- 今開いている addon 一覧
- 特定 addon の node tree
- string table 読取り
- 画面状態の説明

### G. ソーシャル

- フレンド
- FC
- tell
- chat
- event / recruitment

### H. マーケット / 経済

- マーケット閲覧 UI
- 出品 / 購入画面の状態

### I. ハウジング / 外見 / Gpose

- housing UI 状態
- glamour / gearset 周辺 UI
- gpose 関連 UI

### J. 特殊コンテンツ

- Deep Dungeon
- Eureka / Bozja 系
- Gold Saucer
- Island Sanctuary 系
- Cosmic Exploration 系

### K. システム / 設定

- character config
- HUD layout
- keybind / gamepad config
- addon 選択設定

## 3. 対応評価

凡例:

- `Strong`: 現設計で自然に対応可能
- `Partial`: 追加設計は必要だが破綻せず対応可能
- `Weak`: 対応できるが高リスク
- `Out`: 対象外にすべき

| 領域 | 代表ユースケース | 評価 | 理由 |
| --- | --- | --- | --- |
| 基本プレイ | 自キャラ状態確認 | Strong | `get_player_context` で自然に対応 |
| 基本プレイ | duty 文脈取得 | Strong | snapshot tool に向く |
| 戦闘 | 戦闘中かどうか | Strong | coarse status として扱える |
| 戦闘 | 回し支援 / cooldown 詳細 | Out | 設計方針に反する |
| パーティ | パーティ概要取得 | Partial | 他者情報の境界を要精査 |
| クラフター | レシピ UI 読取り | Partial | UI introspection で対応可能 |
| ギャザラー | 採集中状態確認 | Partial | player context 拡張で対応可能 |
| インベントリ | 所持品要約 | Strong | inventory summary 向き |
| 装備 | current gear summary | Partial | read model を追加すれば対応可能 |
| UI | addon tree | Strong | 専用 spec 済み |
| UI | string table | Strong | 専用 spec 済み |
| ソーシャル | Novice Network / tell 読取り | Weak | 高感度、既定では避けるべき |
| ソーシャル | 自動返信 | Out | automation に近い |
| マーケット | 画面読取り | Partial | UI introspection なら可能 |
| マーケット | 自動購入 / 自動出品 | Out | 明確に対象外 |
| ハウジング | housing UI 状態読取り | Partial | allowlist 追加で可能 |
| 外見 | glamour UI 読取り | Partial | UI introspection で可能 |
| Gpose | gpose UI 読取り | Partial | 実装依存は強いが設計上は可能 |
| 特殊コンテンツ | Deep Dungeon 現在文脈 | Partial | duty / UI の組み合わせで可能 |
| 特殊コンテンツ | Eureka/Bozja 固有UI 読取り | Partial | addon allowlist で吸収可能 |
| システム | 設定 UI 読取り | Partial | 高感度寄り、慎重に限定 |

## 4. ユースケースと現在の設計の相性

### かなり相性が良い

- 自キャラ文脈
- inventory summary
- duty / zone context
- addon tree inspection
- string table inspection

### 条件付きで相性が良い

- crafting / gathering support
- special content specific UI
- equipment / gearset summary
- market board UI explanation

### 相性が悪い

- 戦闘最適化
- ソーシャル全文取得
- 自動操作
- 他者追跡

## 5. 網羅性の観点から見た不足

現在の設計で未定義だが、将来的に欲しくなりやすい read-only capability:

- `get_gearset_summary`
- `get_recipe_context`
- `get_gathering_context`
- `get_market_ui_context`
- `get_quest_journal_context`
- `get_map_context`

これらは既存設計を壊さずに足せる候補。

## 6. 破綻しやすいポイント

### ソーシャル系

chat、tell、friend、FC は高感度で、allowlist UI だけでは不十分な可能性がある。

### 特殊コンテンツ乱立

拡張やパッチで UI が増えるため、addon 名ベースの allowlist と snapshot reader の保守負荷は上がる。

### マーケット / 経済

read-only でも、経済活動支援の境界は誤解されやすい。

## 7. 結論

FFXIV の主要ユースケースに対して、現設計は read-only context bridge としては広く対応可能。

破綻しにくいのは:

- 自キャラ
- inventory
- duty
- UI introspection

破綻しやすいのは:

- ソーシャル
- 自動操作
- 高精度戦闘支援

## Sources

- https://na.finalfantasyxiv.com/lodestone/playguide/
- https://na.finalfantasyxiv.com/uiguide/know/how-start/ui_how_to.html
- https://na.finalfantasyxiv.com/game_manual/
- https://na.finalfantasyxiv.com/game_manual/start/
- https://na.finalfantasyxiv.com/lodestone/playguide/event_party_guide/
