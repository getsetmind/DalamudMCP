# Operation Safety Model

## 1. 目的

この文書は、「自プレイヤーの操作」をどこまで設計対象に含めるかを明確にする。

結論から言うと、`DalamudMCP` の baseline には gameplay operation を入れない。

## 2. 本文書の位置づけ

この文書は gameplay automation を baseline に入れないための安全モデルである。

ただし `DalamudMCP` を汎用 action 基盤に拡張する場合、world action と UI action それ自体は `ActionProfile` として扱う余地がある。

## 3. 公式制約

2026-03-19 時点の Dalamud 公式 `Plugin Restrictions` では、次が明示されている。

- plugin は可能な限り、人間ができない操作をしてはならない
- automatic なサーバー interaction は避けるべき
- combat 干渉は強く制限される
- common non-starters として dialog skip、automated crafting、autoroll on loot などが挙げられている

設計への影響:

- 自動操作を MCP tool として一般公開する設計は baseline から除外
- 公式 repo 互換を目指すなら gameplay operation は非採用

## 4. 操作を 3 段階に分ける

### Tier 0: 観測のみ

- context 取得
- UI 読取り

baseline で採用。

### Tier 1: 明示ユーザー操作補助

- plugin UI 上のボタン押下で、その瞬間に限り addon node へ event を流す
- user が現在見えている UI のみ対象
- world action capability を外部から one-shot に呼ぶ場合も、この tier に近い guard が必要

条件:

- MCP からの無人実行は禁止
- user confirmation 必須
- queue / retry / loop 禁止

評価:
private experimental profile としては設計可能。

### Tier 2: gameplay operation

- 自移動
- ターゲット変更
- スキル使用
- dialog 自動送り
- crafting / gathering automation

評価:
不採用。

## 5. `自プレイヤーの操作` の再定義

もし要件の意味が「自キャラへ何らかの行動をさせたい」なら、それはこの設計では採用しない。

もし意味が「今開いている UI で、ユーザー承認の上で一回だけ補助操作したい」なら、Tier 1 の範囲として隔離できる。

## 6. private experimental profile

baseline とは別に、`ExperimentalPrivateProfile` を設計上だけ定義する。

### 条件

- custom repo または dev plugin 前提
- default OFF
- 初回起動時に強い警告
- セッションごとの手動有効化
- allowlist addon のみ
- combat / PvP / cutscene / dialog では無効
- full audit log

### 可能な対象

- 現在表示中 addon の特定 node に対する one-shot event dispatch
- plugin 独自 UI 上での人間確認後の action relay
- one-shot の movement / interaction primitive

### 不可能な対象

- 連続実行
- 自動リトライ
- 条件分岐付きマクロ
- バックグラウンド実行

## 7. MCP にどう露出するか

baseline では action tool を露出しない。

private experimental profile を将来作るとしても:

- tool 名を分離する
- `experimental/unsafe` 名前空間に隔離する
- server metadata でも unsafe profile を明示する
- resources としては公開しない

## 8. 結論

設計として受け入れるのは次の形だけ。

1. UI 読取りは baseline capability
2. world / UI への one-shot action は private experimental `ActionProfile`
3. 自動 workflow や gameplay automation は不採用

これは公式制約と将来の説明責任を考えると外せない境界。

## Sources

- https://dalamud.dev/plugin-publishing/restrictions
- https://dalamud.dev/plugin-development/how-tos/AddonEventManager/
- https://dalamud.dev/plugin-publishing/approval-process/
