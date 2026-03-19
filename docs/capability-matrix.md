# Capability Matrix

この文書は、`DalamudMCP.Plugin` が外部へ公開してよい情報を段階的に切り分けるための表です。

## Phase 0: 設計対象だが未公開

| Capability | 内容 | 想定ソース | 感度 | 初期公開 |
| --- | --- | --- | --- | --- |
| player.identity | 自キャラ名、ホームワールド、現在ジョブ | ClientState / PlayerState | 低 | 可 |
| player.location | テリトリー、マップ、座標概略 | ClientState | 低 | 可 |
| player.status | HP/MP ではなく状態要約、コンテンツ内外、戦闘中か否か | ClientState | 中 | 可 |
| party.summary | パーティ人数、ロール構成、コンテンツ内状況 | PartyList 相当 | 中 | 条件付き |
| target.summary | 現在ターゲットの名称、分類、距離概略 | Target 系サービス | 中 | 条件付き |
| duty.summary | 現在 duty、instance、進行状態の概略 | ClientState / UI 状態 | 中 | 条件付き |
| inventory.summary | カテゴリ別件数、所持金、主要 consumable 集約 | Inventory 系 | 中 | 条件付き |
| ui.summary | 開いている主要アドオン名、選択中 UI の概略 | GameGui / Addon 状態 | 高 | 保留 |
| ui.addonTree | 特定 addon の Atk node tree、可視状態、node 型、node id | AddonLifecycle / NativeWrapper | 高 | 保留 |
| ui.stringTables | addon の StringArray / NumberArray の観測結果 | AddonLifecycle / RequestedUpdate | 高 | 保留 |
| world.movement | world 座標や entity への one-shot 移動 | Movement controller | 高 | Experimental |
| world.interaction | target / nearest object との one-shot interaction | Interaction controller | 高 | Experimental |
| ui.actions | addon allowlist 前提の one-shot UI event | Addon action executor | 高 | Experimental |

## 非公開のまま維持する候補

| Capability | 理由 |
| --- | --- |
| player.input | 自動入力や操作代行に直結する |
| player.movement | 自動移動や bot 化に直結する |
| player.targeting | 戦闘補助や自動化に直結する |
| ui.autoClick | dialog skip や UI automation に直結する |
| combat.telemetry.raw | 戦闘支援、解析、優位性の問題に寄りやすい |
| actor.identifiers.persistent | 他者識別情報の恒久保存に繋がる |
| network.packet_level | DalamudMCP の目的から逸脱する |
| ui.clickable_actions | LLM 経由の操作代行を招く |

## 公開判定ルール

### `可`

- 自キャラ中心
- 要約済み
- 高頻度でなくても成立する
- 外部公開しても自動化と結びつきにくい

### `条件付き`

- 他者情報を含みうる
- 使い方次第で過度な判断支援に寄る
- セッション単位の opt-in や capability toggle が必要
- plugin 設定 UI から個別に有効化できる必要がある

### `保留`

- UI 内部状態への依存が強い
- 安定性が低い
- 将来の tool surface を先に決めないと過剰公開になりやすい

## 初期公開セット案

初期版で本当に出すのは以下だけを推奨する。

1. `get_player_context`
2. `get_player_location`
3. `get_duty_context`
4. `get_inventory_summary`

`party` と `target` は第二段階に回す。

`ui.addonTree` と `ui.stringTables` は設計対象に含めるが、v1 の default profile では無効を推奨する。

加えて、tool / resource / addon allowlist は固定値ではなく設定 UI から選択可能にする。

`world.movement`、`world.interaction`、`ui.actions` は `ActionProfile` にのみ属し、resource としては公開しない。
